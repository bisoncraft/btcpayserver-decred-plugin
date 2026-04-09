using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Decred.Configuration;
using BTCPayServer.Plugins.Decred.RPC;
using BTCPayServer.Plugins.Decred.RPC.Models;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Decred.Services;

public class DecredRpcProvider
{
    readonly ImmutableDictionary<string, JsonRpcClient> _daemonClients;
    readonly ImmutableDictionary<string, JsonRpcClient> _walletClients;
    readonly ILogger<DecredRpcProvider> _logger;
    readonly Dictionary<string, DecredLikeSummary> _summaries = new();

    public class DecredDaemonStateChange
    {
        public string CryptoCode { get; set; }
    }

    public DecredRpcProvider(
        DecredLikeConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<DecredRpcProvider> logger)
    {
        _logger = logger;
        var daemonClients = ImmutableDictionary.CreateBuilder<string, JsonRpcClient>();
        var walletClients = ImmutableDictionary.CreateBuilder<string, JsonRpcClient>();

        foreach (var (cryptoCode, item) in configuration.DecredLikeConfigurationItems)
        {
            var httpClient = httpClientFactory.CreateClient($"{cryptoCode}client");

            daemonClients.Add(cryptoCode, new JsonRpcClient(item.DaemonRpcUri, httpClient));

            var walletHttpClient = httpClientFactory.CreateClient($"{cryptoCode}client");
            walletClients.Add(cryptoCode, new JsonRpcClient(item.WalletRpcUri, walletHttpClient));
        }

        _daemonClients = daemonClients.ToImmutable();
        _walletClients = walletClients.ToImmutable();
    }

    public bool HasSummary(string cryptoCode) => _summaries.ContainsKey(cryptoCode);

    public DecredLikeSummary GetSummary(string cryptoCode)
    {
        _summaries.TryGetValue(cryptoCode, out var summary);
        return summary;
    }

    public IEnumerable<string> GetCryptoCodes() => _daemonClients.Keys;

    public JsonRpcClient GetDaemonClient(string cryptoCode) =>
        _daemonClients.TryGetValue(cryptoCode, out var client) ? client : null;

    public JsonRpcClient GetWalletClient(string cryptoCode) =>
        _walletClients.TryGetValue(cryptoCode, out var client) ? client : null;

    public bool IsAvailable(string cryptoCode) =>
        _summaries.TryGetValue(cryptoCode, out var s) && s.Synced && s.WalletAvailable;

    public async Task UpdateSummary(string cryptoCode, CancellationToken cancellationToken = default)
    {
        if (!_summaries.TryGetValue(cryptoCode, out var summary))
        {
            summary = new DecredLikeSummary();
            _summaries[cryptoCode] = summary;
        }

        var previousAvailable = summary.DaemonAvailable;

        try
        {
            var daemonClient = GetDaemonClient(cryptoCode);
            if (daemonClient == null)
            {
                summary.DaemonAvailable = false;
                return;
            }

            var info = await daemonClient.SendCommandAsync<GetBlockchainInfoResponse>(
                "getblockchaininfo", cancellationToken: cancellationToken);

            summary.CurrentHeight = info.Blocks;
            summary.TargetHeight = info.Headers;
            summary.Synced = info.IsSynced;
            summary.DaemonAvailable = true;
            summary.UpdatedAt = DateTimeOffset.UtcNow;

            // Check wallet
            try
            {
                var walletClient = GetWalletClient(cryptoCode);
                if (walletClient == null)
                {
                    summary.WalletAvailable = false;
                    return;
                }

                var walletInfo = await walletClient.SendCommandAsync<WalletGetInfoResponse>(
                    "getinfo", cancellationToken: cancellationToken);
                summary.WalletHeight = walletInfo.Blocks;
                summary.WalletAvailable = true;
            }
            catch (Exception ex)
            {
                summary.WalletAvailable = false;
                _logger.LogWarning(ex, "Failed to get wallet info for {CryptoCode}", cryptoCode);
            }
        }
        catch (Exception ex)
        {
            summary.DaemonAvailable = false;
            summary.WalletAvailable = false;
            _logger.LogWarning(ex, "Failed to get daemon info for {CryptoCode}", cryptoCode);
        }

        if (previousAvailable != summary.DaemonAvailable)
        {
            _logger.LogInformation("{CryptoCode} daemon availability changed to {Available}",
                cryptoCode, summary.DaemonAvailable);
        }
    }
}

public class DecredLikeSummary
{
    public bool Synced { get; set; }
    public long CurrentHeight { get; set; }
    public long WalletHeight { get; set; }
    public long TargetHeight { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool DaemonAvailable { get; set; }
    public bool WalletAvailable { get; set; }
}
