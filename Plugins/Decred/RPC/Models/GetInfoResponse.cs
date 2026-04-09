using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Decred.RPC.Models;

public class DaemonGetInfoResponse
{
    [JsonProperty("version")]
    public long Version { get; set; }

    [JsonProperty("protocolversion")]
    public int ProtocolVersion { get; set; }

    [JsonProperty("blocks")]
    public long Blocks { get; set; }

    [JsonProperty("timeoffset")]
    public long TimeOffset { get; set; }

    [JsonProperty("connections")]
    public int Connections { get; set; }

    [JsonProperty("difficulty")]
    public double Difficulty { get; set; }

    [JsonProperty("testnet")]
    public bool Testnet { get; set; }

    [JsonProperty("relayfee")]
    public decimal RelayFee { get; set; }
}

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
