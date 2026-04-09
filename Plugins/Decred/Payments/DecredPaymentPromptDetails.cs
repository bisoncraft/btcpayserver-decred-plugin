using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Decred.Payments;

public class DecredPaymentPromptDetails
{
    [JsonProperty("accountName")]
    public string AccountName { get; set; } = "default";

    [JsonProperty("invoiceSettledConfirmationThreshold")]
    public int? InvoiceSettledConfirmationThreshold { get; set; }
}
