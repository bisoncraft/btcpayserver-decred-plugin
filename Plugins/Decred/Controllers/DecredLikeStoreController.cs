using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Decred.Payments;
using BTCPayServer.Plugins.Decred.Services;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Decred.Controllers;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
[Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
[Route("stores/{storeId}/decredlike/{cryptoCode}")]
public class DecredLikeStoreController : Controller
{
    readonly DecredRpcProvider _rpcProvider;
    readonly PaymentMethodHandlerDictionary _handlers;

    public DecredLikeStoreController(
        DecredRpcProvider rpcProvider,
        PaymentMethodHandlerDictionary handlers)
    {
        _rpcProvider = rpcProvider;
        _handlers = handlers;
    }

    StoreData StoreData => HttpContext.GetStoreData();

    [HttpGet]
    public IActionResult GetStoreDecredLikePaymentMethod(string cryptoCode)
    {
        cryptoCode = cryptoCode.ToUpperInvariant();
        var pmi = PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode);
        var storeBlob = StoreData.GetStoreBlob();
        var enabled = !storeBlob.IsExcluded(pmi);

        var config = StoreData.GetPaymentMethodConfig<DecredPaymentPromptDetails>(pmi, _handlers)
            ?? new DecredPaymentPromptDetails();

        var summary = _rpcProvider.GetSummary(cryptoCode);

        return View("Decred/GetStoreDecredLikePaymentMethod", new DecredStoreViewModel
        {
            CryptoCode = cryptoCode,
            Enabled = enabled,
            AccountName = config.AccountName ?? "default",
            InvoiceSettledConfirmationThreshold = config.InvoiceSettledConfirmationThreshold,
            DaemonAvailable = summary?.DaemonAvailable ?? false,
            WalletAvailable = summary?.WalletAvailable ?? false,
            Synced = summary?.Synced ?? false,
            CurrentHeight = summary?.CurrentHeight ?? 0,
            WalletHeight = summary?.WalletHeight ?? 0
        });
    }

    [HttpPost]
    public IActionResult SetStoreDecredLikePaymentMethod(string cryptoCode,
        DecredStoreViewModel model)
    {
        cryptoCode = cryptoCode.ToUpperInvariant();
        var pmi = PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode);

        var storeBlob = StoreData.GetStoreBlob();
        storeBlob.SetExcluded(pmi, !model.Enabled);
        StoreData.SetStoreBlob(storeBlob);

        var promptDetails = new DecredPaymentPromptDetails
        {
            AccountName = model.AccountName ?? "default",
            InvoiceSettledConfirmationThreshold = model.InvoiceSettledConfirmationThreshold
        };

        StoreData.SetPaymentMethodConfig(pmi, JObject.FromObject(promptDetails));

        TempData[WellKnownTempData.SuccessMessage] = $"{cryptoCode} payment method updated.";
        return RedirectToAction(nameof(GetStoreDecredLikePaymentMethod), new { cryptoCode });
    }
}

public class DecredStoreViewModel
{
    public string CryptoCode { get; set; }
    public bool Enabled { get; set; }
    public string AccountName { get; set; } = "default";
    public int? InvoiceSettledConfirmationThreshold { get; set; }
    public bool DaemonAvailable { get; set; }
    public bool WalletAvailable { get; set; }
    public bool Synced { get; set; }
    public long CurrentHeight { get; set; }
    public long WalletHeight { get; set; }
}
