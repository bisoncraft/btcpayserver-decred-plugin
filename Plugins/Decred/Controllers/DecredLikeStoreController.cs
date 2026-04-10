using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Decred.Payments;
using BTCPayServer.Plugins.Decred.RPC;
using BTCPayServer.Plugins.Decred.RPC.Models;
using BTCPayServer.Plugins.Decred.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
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
    readonly StoreRepository _storeRepo;

    public DecredLikeStoreController(
        DecredRpcProvider rpcProvider,
        PaymentMethodHandlerDictionary handlers,
        StoreRepository storeRepo)
    {
        _rpcProvider = rpcProvider;
        _handlers = handlers;
        _storeRepo = storeRepo;
    }

    StoreData StoreData => HttpContext.GetStoreData();

    [HttpGet("")]
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
            WalletAvailable = summary?.WalletAvailable ?? false,
            Synced = summary?.Synced ?? false,
            WalletHeight = summary?.WalletHeight ?? 0
        });
    }

    [HttpPost("")]
    public async Task<IActionResult> SetStoreDecredLikePaymentMethod(string cryptoCode,
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

        await _storeRepo.UpdateStore(StoreData);

        TempData[WellKnownTempData.SuccessMessage] = $"{cryptoCode} payment method updated.";
        var storeId = StoreData.Id;
        return Redirect($"/stores/{storeId}/decredlike/{cryptoCode}");
    }

    [HttpGet("send")]
    public async Task<IActionResult> WalletSend(string cryptoCode)
    {
        cryptoCode = cryptoCode.ToUpperInvariant();
        var walletClient = _rpcProvider.GetWalletClient(cryptoCode);
        if (walletClient == null)
            return NotFound();

        var model = new DecredWalletSendViewModel { CryptoCode = cryptoCode };

        try
        {
            var balance = await walletClient.SendCommandAsync<GetBalanceResponse>(
                "getbalance");
            model.SpendableBalance = balance.TotalSpendable;
            model.TotalBalance = balance.CumulativeTotal;
            model.UnconfirmedBalance = balance.TotalUnconfirmed;
        }
        catch
        {
        }

        return View("Decred/WalletSend", model);
    }

    [HttpPost("send")]
    public async Task<IActionResult> WalletSend(string cryptoCode, DecredWalletSendViewModel model)
    {
        cryptoCode = cryptoCode.ToUpperInvariant();
        model.CryptoCode = cryptoCode;

        var walletClient = _rpcProvider.GetWalletClient(cryptoCode);
        if (walletClient == null)
            return NotFound();

        // Refresh balance for the view.
        try
        {
            var balance = await walletClient.SendCommandAsync<GetBalanceResponse>(
                "getbalance");
            model.SpendableBalance = balance.TotalSpendable;
            model.TotalBalance = balance.CumulativeTotal;
            model.UnconfirmedBalance = balance.TotalUnconfirmed;
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(model.DestinationAddress))
            ModelState.AddModelError(nameof(model.DestinationAddress), "Address is required.");

        if (model.Amount <= 0)
            ModelState.AddModelError(nameof(model.Amount), "Amount must be greater than zero.");

        if (model.Amount > model.SpendableBalance)
            ModelState.AddModelError(nameof(model.Amount), "Amount exceeds spendable balance.");

        // Validate address via dcrwallet.
        if (!string.IsNullOrWhiteSpace(model.DestinationAddress))
        {
            try
            {
                var validation = await walletClient.SendCommandAsync<ValidateAddressResponse>(
                    "validateaddress", new object[] { model.DestinationAddress });
                if (!validation.IsValid)
                    ModelState.AddModelError(nameof(model.DestinationAddress), "Invalid Decred address.");
            }
            catch
            {
                ModelState.AddModelError(nameof(model.DestinationAddress), "Could not validate address.");
            }
        }

        if (!ModelState.IsValid)
            return View("Decred/WalletSend", model);

        try
        {
            var txid = await walletClient.SendCommandAsync<string>(
                "sendtoaddress", new object[] { model.DestinationAddress, model.Amount });

            TempData[WellKnownTempData.SuccessMessage] = $"Transaction sent. TxID: {txid}";
            return RedirectToAction(nameof(WalletSend), new { cryptoCode });
        }
        catch (JsonRpcException ex)
        {
            ModelState.AddModelError(string.Empty, $"Send failed: {ex.Message}");
            return View("Decred/WalletSend", model);
        }
    }
}

public class DecredStoreViewModel
{
    public string CryptoCode { get; set; }
    public bool Enabled { get; set; }
    public string AccountName { get; set; } = "default";
    public int? InvoiceSettledConfirmationThreshold { get; set; }
    public bool WalletAvailable { get; set; }
    public bool Synced { get; set; }
    public long WalletHeight { get; set; }
}

public class DecredWalletSendViewModel
{
    public string CryptoCode { get; set; }
    public string DestinationAddress { get; set; }
    public decimal Amount { get; set; }
    public decimal SpendableBalance { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal UnconfirmedBalance { get; set; }
}
