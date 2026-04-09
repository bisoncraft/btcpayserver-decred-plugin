using System.Globalization;
using BTCPayServer.Payments;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.Decred.Payments;

public class DecredPaymentLinkExtension : IPaymentLinkExtension
{
    readonly DecredLikeSpecificBtcPayNetwork _network;

    public DecredPaymentLinkExtension(DecredLikeSpecificBtcPayNetwork network)
    {
        _network = network;
        PaymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(network.CryptoCode);
    }

    public PaymentMethodId PaymentMethodId { get; }

    public string GetPaymentLink(PaymentPrompt prompt, IUrlHelper urlHelper)
    {
        if (prompt.Destination == null)
            return null;

        var due = prompt.Calculate().Due;
        if (due <= 0)
            return $"{_network.UriScheme}:{prompt.Destination}";

        return $"{_network.UriScheme}:{prompt.Destination}?amount={due.ToString(CultureInfo.InvariantCulture)}";
    }
}
