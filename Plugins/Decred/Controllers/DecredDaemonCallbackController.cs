using BTCPayServer.Plugins.Decred.RPC;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.Decred.Controllers;

[Route("[controller]")]
public class DecredLikeDaemonCallbackController : Controller
{
    readonly EventAggregator _eventAggregator;

    public DecredLikeDaemonCallbackController(EventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    [HttpGet("block")]
    public IActionResult OnBlock(string cryptoCode, string hash)
    {
        _eventAggregator.Publish(new DecredEvent
        {
            CryptoCode = cryptoCode?.ToUpperInvariant() ?? "DCR",
            BlockHash = hash
        });
        return Ok();
    }

    [HttpGet("tx")]
    public IActionResult OnTransaction(string cryptoCode, string hash)
    {
        _eventAggregator.Publish(new DecredEvent
        {
            CryptoCode = cryptoCode?.ToUpperInvariant() ?? "DCR",
            TransactionHash = hash
        });
        return Ok();
    }
}
