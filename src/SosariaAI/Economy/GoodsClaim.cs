using Server;

namespace SosariaAI.Economy;

/// <summary>
/// Goods as somebody described them: the market row, how many (0 when not said), the maker's
/// mark, the magic level and the piece of a suit. A buyer agrees a price on the words, so the item handed over is
/// checked against them.
/// </summary>
public readonly record struct GoodsClaim(GoodsRow Row, int Amount, bool Exceptional, int MagicLevel, ArmorPiece Piece = ArmorPiece.Whole)
{
    /// <summary>The count a price covers: the one said, or the row's usual lot.</summary>
    public int Lot => Row == null ? 0 : Row.IsGear ? 1 : Amount > 0 ? Amount : Row.Lot;

    /// <summary>What the claim is worth at a point in its band.</summary>
    public int Value(int roll) => Appraisal.Value(Row, Lot, Exceptional, MagicLevel, roll, Piece);

    /// <summary>"GM halberd", "vanq halberd", "100 regs".</summary>
    public string Noun => Appraisal.ClaimNoun(this);

    /// <summary>
    /// True when the item is what was described: the same kind, at least the claimed mark and
    /// magic, the named piece of a suit when one was named, and for a stack exactly the agreed
    /// count. A full set takes any piece of the suit: it is bought one piece at a time.
    /// </summary>
    public bool Matches(Item item) =>
        Row != null && item is { Deleted: false } && Appraisal.RowOf(item) == Row &&
        (!Exceptional || Appraisal.IsExceptional(item)) &&
        Appraisal.MagicLevelOf(item) >= MagicLevel &&
        (Piece is ArmorPiece.Whole or ArmorPiece.FullSet || Appraisal.PieceOf(item) == Piece) &&
        (Row.IsGear || item.Amount == Lot);
}
