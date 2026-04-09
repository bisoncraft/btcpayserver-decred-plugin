using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Decred.Payments;

public class DecredLikePaymentData
{
    [JsonProperty("transactionId")]
    public string TransactionId { get; set; }

    [JsonProperty("confirmationCount")]
    public long ConfirmationCount { get; set; }

    [JsonProperty("address")]
    public string Address { get; set; }
}
