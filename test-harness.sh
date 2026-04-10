#!/usr/bin/env bash
# Set env vars matching the dcrdex simnet harness, then run BTCPayServer with the plugin.
# Requires: harness.sh already running (tmux session dcr-harness)
# Uses the trading1 wallet on port 19581.

export BTCPAY_DCR_WALLET_URI="https://127.0.0.1:19581"
export BTCPAY_DCR_RPC_USERNAME="user"
export BTCPAY_DCR_RPC_PASSWORD="pass"

echo "Decred harness env vars set:"
echo "  BTCPAY_DCR_WALLET_URI=$BTCPAY_DCR_WALLET_URI"
echo "  BTCPAY_DCR_RPC_USERNAME=$BTCPAY_DCR_RPC_USERNAME"
echo ""
echo "To run BTCPayServer with the plugin, use:"
echo "  dotnet run --project submodules/btcpayserver/BTCPayServer/BTCPayServer.csproj -- --plugindir=\$(pwd)/Plugins/Decred/bin/Debug/net10.0"
