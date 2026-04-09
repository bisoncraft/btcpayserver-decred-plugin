using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Decred.Payments;
using BTCPayServer.Plugins.Decred.RPC;
using BTCPayServer.Plugins.Decred.RPC.Models;
using BTCPayServer.Services.Invoices;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Decred.Services;

public class DecredListener : EventHostedServiceBase
{
    readonly InvoiceRepository _invoiceRepository;
    readonly DecredRpcProvider _rpcProvider;
    readonly PaymentMethodHandlerDictionary _handlers;
    readonly PaymentService _paymentService;
    readonly ILogger<DecredListener> _logger;

    public DecredListener(
        EventAggregator eventAggregator,
        InvoiceRepository invoiceRepository,
        DecredRpcProvider rpcProvider,
        PaymentMethodHandlerDictionary handlers,
        PaymentService paymentService,
        ILogger<DecredListener> logger) : base(eventAggregator, logger)
    {
        _invoiceRepository = invoiceRepository;
        _rpcProvider = rpcProvider;
        _handlers = handlers;
        _paymentService = paymentService;
        _logger = logger;
    }

    PaymentMethodId Pmi => PaymentTypes.CHAIN.GetPaymentMethodId("DCR");

    protected override void SubscribeToEvents()
    {
        Subscribe<DecredEvent>();
        Subscribe<DecredRpcProvider.DecredDaemonStateChange>();
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case DecredRpcProvider.DecredDaemonStateChange stateChange:
                if (_rpcProvider.IsAvailable(stateChange.CryptoCode))
                    await UpdateAnyPendingPayments(stateChange.CryptoCode, cancellationToken);
                break;

            case DecredEvent { BlockHash: not null } blockEvent:
                await UpdateAnyPendingPayments(blockEvent.CryptoCode, cancellationToken);
                break;

            case DecredEvent { TransactionHash: not null } txEvent:
                await OnTransactionUpdated(txEvent.CryptoCode, txEvent.TransactionHash, cancellationToken);
                break;
        }
    }

    async Task UpdateAnyPendingPayments(string cryptoCode, CancellationToken cancellationToken)
    {
        var invoices = await _invoiceRepository.GetMonitoredInvoices(Pmi, cancellationToken);

        foreach (var invoice in invoices)
        {
            try
            {
                var prompt = invoice.GetPaymentPrompt(Pmi);
                if (prompt?.Destination == null) continue;

                await CheckPaymentsForAddress(cryptoCode, invoice, prompt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating payments for invoice {InvoiceId}", invoice.Id);
            }
        }
    }

    async Task OnTransactionUpdated(string cryptoCode, string txHash, CancellationToken cancellationToken)
    {
        var walletClient = _rpcProvider.GetWalletClient(cryptoCode);
        if (walletClient == null) return;

        try
        {
            var tx = await walletClient.SendCommandAsync<GetTransactionResponse>(
                "gettransaction", [txHash], cancellationToken);

            if (tx.Details == null) return;

            var invoices = await _invoiceRepository.GetMonitoredInvoices(Pmi, cancellationToken);

            foreach (var detail in tx.Details.Where(d => d.Category == "receive"))
            {
                if (detail.Address == null) continue;

                foreach (var invoice in invoices)
                {
                    var prompt = invoice.GetPaymentPrompt(Pmi);
                    if (prompt?.Destination != detail.Address) continue;

                    await HandlePaymentData(cryptoCode, invoice, tx.TxId,
                        detail.Amount, tx.Confirmations, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing transaction {TxHash} for {CryptoCode}", txHash, cryptoCode);
        }
    }

    async Task CheckPaymentsForAddress(string cryptoCode, InvoiceEntity invoice,
        PaymentPrompt prompt, CancellationToken cancellationToken)
    {
        var walletClient = _rpcProvider.GetWalletClient(cryptoCode);
        if (walletClient == null) return;

        try
        {
            var txs = await walletClient.SendCommandAsync<ListTransactionsEntry[]>(
                "listaddresstransactions", [new[] { prompt.Destination }], cancellationToken);

            if (txs == null) return;

            foreach (var tx in txs.Where(t => t.Category == "receive"))
            {
                await HandlePaymentData(cryptoCode, invoice, tx.TxId,
                    tx.Amount, tx.Confirmations, cancellationToken);
            }
        }
        catch (JsonRpcException)
        {
            await CheckPaymentsViaListTransactions(cryptoCode, invoice, prompt, cancellationToken);
        }
    }

    async Task CheckPaymentsViaListTransactions(string cryptoCode, InvoiceEntity invoice,
        PaymentPrompt prompt, CancellationToken cancellationToken)
    {
        var walletClient = _rpcProvider.GetWalletClient(cryptoCode);
        if (walletClient == null) return;

        var txs = await walletClient.SendCommandAsync<ListTransactionsEntry[]>(
            "listtransactions", ["*", (object)1000, (object)0], cancellationToken);

        if (txs == null) return;

        foreach (var tx in txs.Where(t => t.Category == "receive" && t.Address == prompt.Destination))
        {
            await HandlePaymentData(cryptoCode, invoice, tx.TxId,
                tx.Amount, tx.Confirmations, cancellationToken);
        }
    }

    async Task HandlePaymentData(string cryptoCode, InvoiceEntity invoice,
        string txId, decimal amount, long confirmations, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(Pmi, out var handler))
            return;

        var prompt = invoice.GetPaymentPrompt(Pmi);
        if (prompt == null) return;

        var existingPayments = invoice.GetPayments(false);
        var existing = existingPayments.FirstOrDefault(p =>
            p.PaymentMethodId == Pmi &&
            p.Details is JObject details &&
            (string)details["transactionId"] == txId);

        var paymentData = new DecredLikePaymentData
        {
            TransactionId = txId,
            ConfirmationCount = confirmations,
            Address = prompt.Destination
        };

        var status = GetPaymentStatus(confirmations, prompt);

        if (existing != null)
        {
            var existingData = existing.Details?.ToObject<DecredLikePaymentData>();
            if (existingData != null && existingData.ConfirmationCount != confirmations)
            {
                existing.Status = status;
                existing.Details = JToken.FromObject(paymentData);
                await _paymentService.UpdatePayments([existing]);
            }
            return;
        }

        var newPayment = new PaymentData
        {
            Id = $"{txId}#{prompt.Destination}",
            Created = DateTimeOffset.UtcNow,
            Status = status,
            Amount = amount,
            Currency = cryptoCode
        };
        newPayment.Set(invoice, handler, paymentData);

        await _paymentService.AddPayment(newPayment);

        _logger.LogInformation(
            "Payment detected for invoice {InvoiceId}: {Amount} DCR, tx {TxId}, {Confirmations} confirmations",
            invoice.Id, amount, txId, confirmations);
    }

    static PaymentStatus GetPaymentStatus(long confirmations, PaymentPrompt prompt)
    {
        var promptDetails = prompt.Details?.ToObject<DecredPaymentPromptDetails>();
        int requiredConfirmations;

        if (promptDetails?.InvoiceSettledConfirmationThreshold != null)
        {
            requiredConfirmations = promptDetails.InvoiceSettledConfirmationThreshold.Value;
        }
        else
        {
            requiredConfirmations = 1;
        }

        return confirmations >= requiredConfirmations
            ? PaymentStatus.Settled
            : PaymentStatus.Processing;
    }
}
