using System.Collections.Generic;
using System.Linq;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Bitcoin;

namespace BTCPayServer.Plugins.Decred.Payments;

public class DecredCheckoutModelExtension : ICheckoutModelExtension
{
    readonly DecredLikeSpecificBtcPayNetwork _network;
    readonly IPaymentLinkExtension _paymentLinkExtension;

    public DecredCheckoutModelExtension(
        DecredLikeSpecificBtcPayNetwork network,
        IEnumerable<IPaymentLinkExtension> paymentLinkExtensions)
    {
        _network = network;
        PaymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(network.CryptoCode);
        _paymentLinkExtension = paymentLinkExtensions.Single(p => p.PaymentMethodId == PaymentMethodId);
    }

    public PaymentMethodId PaymentMethodId { get; }
    public string Image => _network.CryptoImagePath;
    public string Badge => "";

    public void ModifyCheckoutModel(CheckoutModelContext context)
    {
        context.Model.CheckoutBodyComponentName = BitcoinCheckoutModelExtension.CheckoutBodyComponentName;
        context.Model.ShowRecommendedFee = false;

        var paymentData = context.InvoiceEntity.GetPayments(false)
            .Where(p => p.PaymentMethodId == PaymentMethodId)
            .Select(p => p.Details?.ToObject<DecredLikePaymentData>())
            .Where(d => d != null)
            .MinBy(d => d.ConfirmationCount);

        if (paymentData != null)
        {
            var promptDetails = context.Handler.ParsePaymentPromptDetails(context.Prompt.Details)
                as DecredPaymentPromptDetails;
            context.Model.ReceivedConfirmations = paymentData.ConfirmationCount;
            context.Model.RequiredConfirmations = promptDetails?.InvoiceSettledConfirmationThreshold ?? 1;
        }

        var paymentLink = _paymentLinkExtension.GetPaymentLink(context.Prompt, context.UrlHelper);
        context.Model.InvoiceBitcoinUrl = paymentLink ?? "";
        context.Model.InvoiceBitcoinUrlQR = paymentLink ?? "";
    }
}
