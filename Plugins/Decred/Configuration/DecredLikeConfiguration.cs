using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.Decred.Configuration;

public class DecredLikeConfiguration
{
    public Dictionary<string, DecredLikeConfigurationItem> DecredLikeConfigurationItems { get; set; } = [];
}

public class DecredLikeConfigurationItem
{
    public Uri WalletRpcUri { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}
