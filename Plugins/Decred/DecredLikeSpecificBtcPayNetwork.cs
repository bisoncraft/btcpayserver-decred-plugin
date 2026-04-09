namespace BTCPayServer.Plugins.Decred;

public class DecredLikeSpecificBtcPayNetwork : BTCPayNetworkBase
{
    public int MaxTrackedConfirmation { get; set; } = 10;
    public string UriScheme { get; set; }
}
