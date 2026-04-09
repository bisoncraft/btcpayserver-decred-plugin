namespace BTCPayServer.Plugins.Decred.RPC;

public class DecredEvent
{
    public string CryptoCode { get; set; }
    public string BlockHash { get; set; }
    public string TransactionHash { get; set; }
}
