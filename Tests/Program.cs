using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Decred.RPC;
using BTCPayServer.Plugins.Decred.RPC.Models;

// Test the Decred JSON-RPC client against the dcrdex simnet harness.
// Requires: harness.sh running (tmux session dcr-harness)
// Uses the trading1 wallet on port 19581.

var walletUri = new Uri("https://127.0.0.1:19581");
var username = "user";
var password = "pass";

var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
var httpClient = new HttpClient(handler);
var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

var wallet = new JsonRpcClient(walletUri, httpClient);

var passed = 0;
var failed = 0;

async Task Test(string name, Func<Task> test)
{
    try
    {
        await test();
        Console.WriteLine($"  PASS: {name}");
        passed++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL: {name} - {ex.Message}");
        failed++;
    }
}

Console.WriteLine("Testing Decred RPC client against simnet harness (trading1 wallet)...\n");

Console.WriteLine("=== dcrwallet ===");

await Test("getinfo", async () =>
{
    var info = await wallet.SendCommandAsync<WalletGetInfoResponse>("getinfo");
    Console.WriteLine($"         version={info.Version}, blocks={info.Blocks}, balance={info.Balance}");
    if (info.Blocks < 1) throw new Exception("Block count should be > 0");
});

await Test("getbalance", async () =>
{
    var balance = await wallet.SendCommandAsync<GetBalanceResponse>("getbalance");
    Console.WriteLine($"         spendable={balance.TotalSpendable}, total={balance.CumulativeTotal}");
    if (balance.TotalSpendable <= 0) throw new Exception("Should have spendable balance");
});

await Test("getnewaddress", async () =>
{
    var address = await wallet.SendCommandAsync<string>("getnewaddress", ["default", "ignore"]);
    Console.WriteLine($"         address={address}");
    if (!address.StartsWith("Ss")) throw new Exception($"Simnet address should start with Ss, got {address}");
});

await Test("getnewaddress + sendtoaddress + listaddresstransactions", async () =>
{
    // Generate a fresh address
    var addr = await wallet.SendCommandAsync<string>("getnewaddress", ["default", "ignore"]);
    Console.WriteLine($"         generated address: {addr}");

    // Send some DCR to it
    var txid = await wallet.SendCommandAsync<string>("sendtoaddress", [addr, (object)2.5m]);
    Console.WriteLine($"         sent 2.5 DCR, txid: {txid}");

    // Check with listaddresstransactions
    var txs = await wallet.SendCommandAsync<ListTransactionsEntry[]>(
        "listaddresstransactions", [new[] { addr }]);
    Console.WriteLine($"         listaddresstransactions found {txs.Length} tx(s)");
    if (txs.Length < 1) throw new Exception("Should find at least 1 transaction");

    var tx = txs[0];
    Console.WriteLine($"         txid={tx.TxId}, amount={tx.Amount}, confirmations={tx.Confirmations}, category={tx.Category}");
    if (tx.Amount != 2.5m) throw new Exception($"Expected 2.5, got {tx.Amount}");
    if (tx.Category != "receive") throw new Exception($"Expected 'receive', got {tx.Category}");
});

await Test("gettransaction", async () =>
{
    // Get last transaction
    var txs = await wallet.SendCommandAsync<ListTransactionsEntry[]>(
        "listtransactions", ["*", (object)1, (object)0]);
    if (txs.Length == 0) throw new Exception("No transactions found");

    var tx = await wallet.SendCommandAsync<GetTransactionResponse>("gettransaction", [txs[0].TxId]);
    Console.WriteLine($"         txid={tx.TxId}, confirmations={tx.Confirmations}, details={tx.Details?.Count ?? 0} entries");
    if (tx.TxId == null) throw new Exception("Transaction ID should not be null");
});

Console.WriteLine($"\n=== Results: {passed} passed, {failed} failed ===");
return failed > 0 ? 1 : 0;
