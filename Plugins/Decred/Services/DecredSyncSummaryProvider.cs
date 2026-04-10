using System.Collections.Generic;
using System.Linq;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Payments;

namespace BTCPayServer.Plugins.Decred.Services;

public class DecredSyncSummaryProvider : ISyncSummaryProvider
{
    readonly DecredRpcProvider _rpcProvider;

    public DecredSyncSummaryProvider(DecredRpcProvider rpcProvider)
    {
        _rpcProvider = rpcProvider;
    }

    public bool AllAvailable()
    {
        return _rpcProvider.GetCryptoCodes().All(c =>
        {
            var summary = _rpcProvider.GetSummary(c);
            return summary is { WalletAvailable: true, Synced: true };
        });
    }

    public string Partial => "Decred/DecredSyncSummary";

    public IEnumerable<ISyncStatus> GetStatuses()
    {
        return _rpcProvider.GetCryptoCodes().Select(c =>
        {
            var summary = _rpcProvider.GetSummary(c);
            return new DecredSyncStatus
            {
                PaymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(c).ToString(),
                CryptoCode = c,
                Summary = summary
            };
        });
    }
}

public class DecredSyncStatus : ISyncStatus
{
    public string PaymentMethodId { get; set; }
    public string CryptoCode { get; set; }
    public DecredLikeSummary Summary { get; set; }
    public bool Available => Summary is { WalletAvailable: true, Synced: true };
}
