namespace BTCPayServer.Plugins.Decred.Utils;

public static class DecredMoney
{
    // Decred uses 8 decimal places (atoms), same as Bitcoin satoshis
    public const long Divisibility = 100_000_000;

    public static decimal Convert(long atoms)
    {
        return (decimal)atoms / Divisibility;
    }

    public static long Convert(decimal dcr)
    {
        return (long)(dcr * Divisibility);
    }
}
