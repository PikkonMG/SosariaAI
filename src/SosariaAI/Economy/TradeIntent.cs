namespace SosariaAI.Economy;

/// <summary>What a line means to a trade.</summary>
public enum TradeIntentKind
{
    None,

    /// <summary>"what are you selling", "what u got".</summary>
    AskStock,

    /// <summary>"how much", "ulric how much", "price on the hally".</summary>
    AskPrice,

    /// <summary>A number put on the table: "3500", "4k", "ill go 3k".</summary>
    Offer,

    /// <summary>"deal", "ill take it", "ok" inside a haggle.</summary>
    Accept,

    /// <summary>"nvm", "too much", "no thanks".</summary>
    Decline,

    /// <summary>A shout putting goods up: "WTS GM halberd 5k".</summary>
    Sell,

    /// <summary>An answer to a WTB: "i have one", "i have one 5k".</summary>
    HaveOne,

    /// <summary>A shout for goods wanted: "wtb GM plate chest", "looking for a gm katana".</summary>
    Want,

    /// <summary>A request for work: "can you make me a gm katana", "do you take orders".</summary>
    Order,

    /// <summary>A question after an order: "is my order ready", "here to pick up".</summary>
    OrderStatus
}

/// <summary>
/// One line read as trade talk. <see cref="Price"/> is 0 when no number was named;
/// <see cref="Goods"/> carries the goods when the line named any. <see cref="Sure"/> is false
/// when the words smell of trade but no rule placed them.
/// </summary>
public readonly record struct TradeIntent(
    TradeIntentKind Kind,
    int Price,
    GoodsClaim? Goods,
    bool Sure
)
{
    public static readonly TradeIntent Nothing = new(TradeIntentKind.None, 0, null, true);

    public static readonly TradeIntent Unsure = new(TradeIntentKind.None, 0, null, false);

    public bool IsTrade => Kind != TradeIntentKind.None;
}
