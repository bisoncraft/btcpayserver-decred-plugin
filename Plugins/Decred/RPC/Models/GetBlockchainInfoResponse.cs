using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Decred.RPC.Models;

public class GetBlockchainInfoResponse
{
    [JsonProperty("chain")]
    public string Chain { get; set; }

    [JsonProperty("blocks")]
    public long Blocks { get; set; }

    [JsonProperty("headers")]
    public long Headers { get; set; }

    [JsonProperty("bestblockhash")]
    public string BestBlockHash { get; set; }

    [JsonProperty("syncheight")]
    public long SyncHeight { get; set; }

    [JsonProperty("verificationprogress")]
    public double VerificationProgress { get; set; }

    public bool IsSynced => VerificationProgress > 0.999;
}
