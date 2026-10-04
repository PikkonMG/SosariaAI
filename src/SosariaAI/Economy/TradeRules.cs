namespace SosariaAI.Economy;

/// <summary>
/// Whose goods and gold a character may ever move. A character's own errands (vendors, gear
/// upgrades) deal with shopkeepers, never with a person at a keyboard. A person trades face to
/// face and hands goods or gold over by dropping them on the character; the character never
/// reaches into that person's pack or bank.
/// </summary>
public static class TradeRules
{
    public static bool MayBeCounterpart(bool isPlayer) => !isPlayer;
}
