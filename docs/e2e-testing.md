# BTCPay Server Decred Plugin - End-to-End Test

## Prerequisites

- .NET 10 SDK
- Go (to build dcrd/dcrwallet/dcrctl)
- PostgreSQL running
- tmux
- jq

## 1. Build Decred binaries

```bash
# Skip if you already have dcrd, dcrwallet, dcrctl in your PATH
git clone https://github.com/decred/dcrd && cd dcrd && go install . ./cmd/... && cd ..
git clone https://github.com/decred/dcrwallet && cd dcrwallet && go install . && cd ..
git clone https://github.com/decred/dcrctl && cd dcrctl && go install . && cd ..
```

## 2. Start the simnet harness

```bash
git clone https://github.com/decred/dcrdex
cd dcrdex/dex/testing/dcr
bash harness.sh
```

This opens a tmux session. Open a **new terminal** for the remaining steps.
The harness provides:

- dcrd RPC at `https://127.0.0.1:19561` (user: `user`, pass: `pass`)
- trading1 wallet RPC at `https://127.0.0.1:19581` (user: `user`, pass: `pass`)
- TLS certs at `~/dextest/dcr/{alpha,trading1}/rpc.cert`

Verify it's running:

```bash
dcrctl --simnet -s 127.0.0.1:19561 -u user -P pass \
  -c ~/dextest/dcr/alpha/rpc.cert getblockcount
```

## 3. Clone and build the plugin

```bash
git clone --recursive https://github.com/<your-user>/btcpayserver-decred-plugin
cd btcpayserver-decred-plugin

# Workaround for NETSDK1226 on some .NET 10 SDK versions
echo '<Project><PropertyGroup><AllowMissingPrunePackageData>true</AllowMissingPrunePackageData></PropertyGroup></Project>' > submodules/btcpayserver/Directory.Build.props

dotnet build Plugins/Decred/BTCPayServer.Plugins.Decred.csproj
```

## 4. Run the RPC integration test (optional sanity check)

```bash
dotnet run --project Tests/HarnessTest.csproj
```

All 8 tests should pass.

## 5. Set up PostgreSQL for BTCPayServer

```bash
# Create a database (skip if you already have one)
sudo -u postgres createdb btcpaytest
sudo -u postgres psql -c "CREATE USER btcpay WITH PASSWORD 'btcpay';"
sudo -u postgres psql -c "GRANT ALL ON DATABASE btcpaytest TO btcpay;"
sudo -u postgres psql -c "ALTER DATABASE btcpaytest OWNER TO btcpay;"
```

## 6. Install the plugin and run BTCPayServer

```bash
cd btcpayserver-decred-plugin

# Set Decred harness env vars (trading1 wallet)
export BTCPAY_DCR_WALLET_URI="https://127.0.0.1:19581"
export BTCPAY_DCR_RPC_USERNAME="user"
export BTCPAY_DCR_RPC_PASSWORD="pass"

# Set BTCPayServer config
export BTCPAY_POSTGRES="Host=localhost;Database=btcpaytest;Username=btcpay;Password=btcpay"
export BTCPAY_NETWORK="regtest"
export BTCPAY_DEBUGLOG="debug.log"

# Build the plugin DLL
dotnet build Plugins/Decred/BTCPayServer.Plugins.Decred.csproj -c Debug

# Copy plugin into the standard plugin directory
mkdir -p ~/.btcpayserver/Plugins/BTCPayServer.Plugins.Decred
cp Plugins/Decred/bin/Debug/net10.0/* ~/.btcpayserver/Plugins/BTCPayServer.Plugins.Decred/

# Run BTCPayServer (--no-launch-profile avoids launchSettings.json overriding env vars)
dotnet run --no-launch-profile --project submodules/btcpayserver/BTCPayServer/BTCPayServer.csproj
```

BTCPayServer should start at `http://localhost:23002` (regtest default port).

Look for these lines in the output to confirm the plugin loaded:
```
Running plugin BTCPayServer.Plugins.Decred - 1.0.0.0
Supported chains: BTC,DCR
DCR daemon availability changed to True
```

## 7. Configure BTCPayServer

1. Open `http://localhost:23002` in a browser
2. Create an admin account
3. Create a store
4. Go to the store settings
5. Click "Decred" in the left sidebar
6. Check "Enable DCR payments" and click Save

The settings page shows daemon and wallet status. Both should show "Available".

## 8. Test a payment

### Create an invoice

1. In the store, go to Invoices > Create Invoice
2. Set the currency to DCR and amount to 1
3. Create the invoice
4. The invoice page should show a Decred payment address (starts with `Ss` on simnet)

### Send DCR to it

In a terminal (not the tmux harness):

```bash
CERT=~/dextest/dcr/alpha/rpc.cert
ADDR="<paste the address from the invoice>"

# Send DCR from the alpha wallet
dcrctl --simnet -s 127.0.0.1:19562 -u user -P pass -c $CERT \
  --wallet sendtoaddress $ADDR 1.0

# Mine a block to confirm it
cd ~/dextest/dcr/harness-ctl && ./mine-alpha 1
```

### Verify

1. Wait up to 15 seconds (the plugin polls every 15s)
2. The invoice should show the payment as detected
3. After mining a block, the invoice should move to "Settled" status

## 9. Test sending from the wallet

The plugin is connected to the trading1 wallet. First, fund it from alpha:

```bash
CERT=~/dextest/dcr/trading1/rpc.cert

# Get an address from trading1
TRADING1_ADDR=$(dcrctl --simnet -s 127.0.0.1:19581 -u user -P pass -c $CERT \
  --wallet getnewaddress)

# Send DCR from alpha to trading1
dcrctl --simnet -s 127.0.0.1:19562 -u user -P pass \
  -c ~/dextest/dcr/alpha/rpc.cert --wallet sendtoaddress $TRADING1_ADDR 10.0

# Mine a block to confirm
cd ~/dextest/dcr/harness-ctl && ./mine-alpha 1
```

Then test the send page:

1. In the store settings, click "Decred" in the sidebar
2. Click the "Send" button in the Wallet card
3. The page should show the spendable balance
4. Get a destination address from alpha:
   ```bash
   dcrctl --simnet -s 127.0.0.1:19562 -u user -P pass \
     -c ~/dextest/dcr/alpha/rpc.cert --wallet getnewaddress
   ```
5. Enter the address and an amount, then click "Send Transaction"
6. A success message with the transaction ID should appear

Note: the harness trading1 wallet is already unlocked, so sending works without setting `BTCPAY_DCR_WALLET_PASSPHRASE`. In production Docker deployments, that env var must be set.

## Troubleshooting

**Plugin not loading:**
- Check the startup output for "Running plugin BTCPayServer.Plugins.Decred"
- Make sure the DLL was copied to `~/.btcpayserver/Plugins/BTCPayServer.Plugins.Decred/`
- If the plugin was disabled due to a crash, clear the state:
  ```bash
  rm -f ~/.btcpayserver/Plugins/commands
  ```

**Plugin crashed at startup (disabled on next run):**
- Clear the disabled state and recopy:
  ```bash
  rm -f ~/.btcpayserver/Plugins/commands
  cp Plugins/Decred/bin/Debug/net10.0/* ~/.btcpayserver/Plugins/BTCPayServer.Plugins.Decred/
  ```

**Daemon shows as unavailable:**
- Verify env vars are set correctly (use `env | grep BTCPAY_DCR`)
- The harness uses TLS with self-signed certs - the plugin accepts any cert by default
- Check that the harness is still running: `tmux has-session -t dcr-harness`

**No Decred option in store settings:**
- The plugin may not be loaded - check startup logs
- The sidebar link goes to `/stores/{storeId}/decredlike/DCR`

**Invoice doesn't detect payment:**
- The listener polls every 15 seconds - wait a moment
- Mine a block to trigger confirmation: `cd ~/dextest/dcr/harness-ctl && ./mine-alpha 1`
- Check BTCPayServer logs for "Payment detected" messages
- You can also trigger a check via the callback endpoint:
  ```bash
  curl http://localhost:23002/DecredLikeDaemonCallback/block?cryptoCode=DCR&hash=test
  ```

**Store settings don't save (404 on Save):**
- Make sure you rebuilt and recopied the plugin after the latest changes
- Clear browser cache or try incognito

**Fresh start (reset everything):**
```bash
# Stop BTCPayServer (Ctrl+C)
# Drop and recreate the database
sudo -u postgres dropdb btcpaytest && sudo -u postgres createdb -O btcpay btcpaytest
# Clear plugin state
rm -rf ~/.btcpayserver/Plugins/BTCPayServer.Plugins.Decred
rm -f ~/.btcpayserver/Plugins/commands
# Rebuild, recopy, and start again
dotnet build Plugins/Decred/BTCPayServer.Plugins.Decred.csproj -c Debug
mkdir -p ~/.btcpayserver/Plugins/BTCPayServer.Plugins.Decred
cp Plugins/Decred/bin/Debug/net10.0/* ~/.btcpayserver/Plugins/BTCPayServer.Plugins.Decred/
dotnet run --no-launch-profile --project submodules/btcpayserver/BTCPayServer/BTCPayServer.csproj
```
