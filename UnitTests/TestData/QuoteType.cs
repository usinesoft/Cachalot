namespace Tests.TestData
{
    public enum QuoteType
    {
        INVALID,
        YIELD,
        PRICE,
        PRICE32, // for some american bonds
        PRICE64, // for some american bonds
        FUTURE,
        SPREAD,
        RATE,
        VOLATILITY
    }
}