using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Decred.RPC.Models;

public class WalletGetInfoResponse
{
    [JsonProperty("version")]
    public long Version { get; set; }

    [JsonProperty("blocks")]
    public long Blocks { get; set; }

    [JsonProperty("balance")]
    public decimal Balance { get; set; }

    [JsonProperty("txfee")]
    public decimal TxFee { get; set; }
}
