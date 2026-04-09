namespace BTCPayServer.Plugins.Decred.ViewModels;

public class DecredPaymentViewModel
{
    public string TransactionId { get; set; }
    public long ConfirmationCount { get; set; }
    public string Address { get; set; }
    public decimal Amount { get; set; }
    public string TransactionLink { get; set; }
}
