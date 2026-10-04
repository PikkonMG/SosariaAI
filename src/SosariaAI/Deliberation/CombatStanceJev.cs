using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Deliberation;

/// <summary>Jev's read of a fight: a stance, or null when the rules must pick.</summary>
public readonly record struct StanceVerdict(CombatStance? Stance, double Confidence, string Source);

/// <summary>
/// The tactical advisor for a fight that matters. Code owns the fight: it computes every
/// number, the trade of blows included, and passes Jev only a few short lines of words (hurt,
/// next to you, winning the trade, cornered). One request asks one yes/no question for each
/// stance the character can carry out, and the stance with the clearest yes wins. Small System
/// One models (Von, Laya) judge one yes/no far better than a pick among seven: on a seven-way
/// choice they spread their answer thin and lean on shared words. A yes below its risk floor,
/// or one that does not clearly beat the next, is no answer, and the stance rules pick. Asked
/// only for a fight that matters (<see cref="StanceRules.WorthAsking"/>), only when the words
/// changed, at most once per <see cref="AskGapMs"/> per character and
/// <see cref="MaxAsksPerFight"/> times a fight.
/// </summary>
public static class CombatStanceJev
{
    /// <summary>At most one ask per character this often.</summary>
    public const int AskGapMs = 5000;

    /// <summary>A stance Jev set stands this long, then the rules take over until the next answer.</summary>
    public const int StanceHoldMs = 6000;

    /// <summary>Asks one fight may make; past it the rules run the rest of the fight.</summary>
    public const int MaxAsksPerFight = 6;

    public const double HealthyFraction = 0.9;
    public const double LightlyHurtFraction = 0.6;
    public const double BadlyHurtFraction = 0.3;
    public const double PlentyManaFraction = 0.7;
    public const double HalfManaFraction = 0.35;
    public const double LowManaFraction = 0.1;
    public const int NextToYouTiles = 1;
    public const int CloseTiles = 3;
    public const int FewStepsTiles = 7;
    public const int One = 1;

    private const string WordSeparator = "|";
    private const string ListSeparator = ", ";

    private const string YesKey = "true";
    private const string NoKey = "false";

    private sealed record StanceAsk(CombatStance Stance, string Question, string Yes, string No);

    // One yes/no per stance, keyed by the question name. The wording was checked against Laya
    // and Von: shorter rubrics lost right answers. Every word is paid for on every Jev stance
    // call, and Laya's English model reads at most 512 tokens.
    private static readonly IReadOnlyDictionary<string, StanceAsk> StanceAsks = new Dictionary<string, StanceAsk>
    {
        ["press"] = new(CombatStance.Press, "Should you keep attacking this foe?",
            "you are winning, or the foe is weak or hurt", "you are losing badly or near death"),
        ["hold"] = new(CombatStance.Hold, "Should you stand your ground and let the foe come to you?",
            "friends fight beside you or you guard a narrow spot", "you fight alone in the open"),
        ["kite"] = new(CombatStance.Kite, "Should you step back out of reach and shoot or cast from range?",
            "you are an archer or mage and the foe fights up close", "you are a warrior, or the foe also fights from range"),
        ["heal"] = new(CombatStance.Heal, "Should you stop and heal yourself right now?",
            "you are badly hurt and have bandages, potions, or heal spells", "you are unhurt or have no way to heal"),
        ["paralyze_then_kite"] = new(CombatStance.ParalyzeThenKite, "Should you freeze the foe with Paralyze and walk away?",
            "you are a mage with Paralyze and a melee foe is next to you", "you cannot cast Paralyze or the foe fights from range"),
        ["protect"] = new(CombatStance.Protect, "Should you raise your protective ward spell now?",
            "your spells keep getting broken and you have a ward spell", "your spells go through or the ward is already up"),
        ["flee"] = new(CombatStance.Flee, "Should you run away from this fight right now?",
            "you are near death or badly outnumbered and cannot heal", "you can still win, heal, or hold your ground")
    };

    /// <summary>Set by the Brain when a System One provider is routed. Returns true when it took the call.</summary>
    internal static Func<JevCall, bool> Asker { get; set; }

    /// <summary>True once the Brain has a provider to ask; until then the stance rules alone run the fight.</summary>
    public static bool IsAvailable => Asker != null;

    public static JevDecision Build(StanceSituation s)
    {
        var state = new Dictionary<string, object>
        {
            ["you"] = YouWords(s),
            ["foe"] = FoeWords(s),
            ["fight"] = FightWords(s)
        };

        var means = MeansWords(s);

        if (means != null)
        {
            state["you_have"] = means;
        }

        var questions = new Dictionary<string, JevQuestion>();

        foreach (var (name, ask) in StanceAsks)
        {
            if (StanceRules.Feasible(ask.Stance, s))
            {
                questions[name] = new JevQuestion(
                    SystemOneApi.NoulType,
                    ask.Question,
                    new Dictionary<string, string> { [YesKey] = ask.Yes, [NoKey] = ask.No }
                );
            }
        }

        return new JevDecision(state, questions);
    }

    /// <summary>The words Jev would read. The same words give the same answer, so they are not asked twice.</summary>
    public static string Key(StanceSituation s) =>
        string.Join(WordSeparator, YouWords(s), FoeWords(s), FightWords(s), MeansWords(s));

    /// <summary>
    /// The feasible stance with the highest yes, when that yes clears its risk floor and beats
    /// the next yes by <see cref="StanceRules.ClearWinGap"/>; else no stance. Either way the
    /// verdict names the provider that was asked.
    /// </summary>
    internal static StanceVerdict Read(
        IReadOnlyDictionary<string, SystemOneAnswer> answers,
        StanceSituation s,
        string providerName = BrainProviders.JevName
    )
    {
        var source = string.IsNullOrWhiteSpace(providerName) ? BrainProviders.JevName : providerName;

        CombatStance? best = null;
        var bestYes = 0.0;
        var nextYes = 0.0;

        if (answers != null)
        {
            foreach (var (name, ask) in StanceAsks)
            {
                if (!StanceRules.Feasible(ask.Stance, s) ||
                    !answers.TryGetValue(name, out var answer) || answer.Noul is not { } yes)
                {
                    continue;
                }

                if (best == null || yes > bestYes)
                {
                    nextYes = best == null ? nextYes : bestYes;
                    best = ask.Stance;
                    bestYes = yes;
                }
                else
                {
                    nextYes = Math.Max(nextYes, yes);
                }
            }
        }

        return best is { } stance && bestYes >= StanceRules.MinYes(stance) && bestYes - nextYes >= StanceRules.ClearWinGap
            ? new StanceVerdict(stance, bestYes, source)
            : new StanceVerdict(null, bestYes, source);
    }

    /// <summary>
    /// Puts the fight to Jev. False when no System One provider is routed or the call was
    /// refused (the budget, the queue); the stance rules keep the fight meanwhile.
    /// </summary>
    public static bool TryAsk(SosariaCharacter character, Mobile foe, StanceSituation s, Action<StanceVerdict> onVerdict)
    {
        var asker = Asker;

        if (asker == null || character == null || foe == null)
        {
            return false;
        }

        return asker(
            new JevCall(
                character,
                foe,
                BrainEventKind.Attacked,
                JevBudgetRules.FightKind(s.FoeIsPerson),
                Build(s),
                (answers, providerName) => onVerdict(Read(answers, s, providerName))
            )
        );
    }

    public static string HealthWord(double fraction) =>
        fraction >= HealthyFraction ? "unhurt" :
        fraction >= LightlyHurtFraction ? "lightly hurt" :
        fraction >= BadlyHurtFraction ? "badly hurt" : "near death";

    public static string ManaWord(double fraction) =>
        fraction >= PlentyManaFraction ? "plenty" :
        fraction >= HalfManaFraction ? "half" :
        fraction >= LowManaFraction ? "low" : "empty";

    public static string DistanceWord(int tiles) =>
        tiles <= NextToYouTiles ? "next to you" :
        tiles <= CloseTiles ? "close" :
        tiles <= FewStepsTiles ? "a few steps away" : "far";

    public static string CountWord(int count) => count <= 0 ? "no" : count == One ? "one" : "several";

    /// <summary>"no enemy", "one enemy", "several enemies".</summary>
    public static string Counted(int count, string one, string many) => $"{CountWord(count)} {(count > One ? many : one)}";

    public static string InterruptWord(int count) => count <= 0 ? "none" : count == One ? "once" : "again and again";

    /// <summary>The trade of blows as a player would say it.</summary>
    public static string TradeWord(FightOutlook outlook) =>
        outlook switch
        {
            FightOutlook.Winning => "winning the trade",
            FightOutlook.Losing => "losing the trade",
            FightOutlook.Even => "an even trade",
            _ => "just started"
        };

    /// <summary>"mage, badly hurt, mana half, poisoned".</summary>
    public static string YouWords(StanceSituation s)
    {
        var words = $"{StyleWord(s.Style, s.Caster, s.TankMage)}, {HealthWord(s.HitsFraction)}";

        if (s.Caster)
        {
            words += $", mana {ManaWord(s.ManaFraction)}";
        }

        return s.Poisoned ? words + ", poisoned" : words;
    }

    /// <summary>"a person who fights up close, lightly hurt, next to you, casting a spell".</summary>
    public static string FoeWords(StanceSituation s) =>
        $"{(s.FoeIsPerson ? "a person" : "a creature")} who {FoeStyleWord(s.FoeStyle)}, {HealthWord(s.FoeHitsFraction)}, " +
        $"{DistanceWord(s.FoeDistance)}, {DoingWord(s.FoeCasting, s.FoeHeld)}";

    /// <summary>"winning the trade, taking light damage, several enemies next to you, spells broken once, cornered, one friend fighting".</summary>
    public static string FightWords(StanceSituation s)
    {
        var words = new List<string> { TradeWord(s.Outlook) };

        if (s.LightDamage)
        {
            words.Add("taking light damage");
        }

        words.Add($"{Counted(s.AdjacentFoes, "enemy", "enemies")} next to you");

        if (s.RecentInterrupts > 0)
        {
            words.Add($"spells broken {InterruptWord(s.RecentInterrupts)}");
        }

        if (s.Cornered)
        {
            words.Add("cornered");
        }

        if (s.AlliesNear > 0)
        {
            words.Add($"{Counted(s.AlliesNear, "friend", "friends")} fighting");
        }

        return string.Join(ListSeparator, words);
    }

    /// <summary>What the character can do beyond hitting, or null when nothing.</summary>
    public static string MeansWords(StanceSituation s)
    {
        var means = new List<string>();

        if (s.Warded)
        {
            means.Add("ward already up");
        }
        else if (s.CanWard)
        {
            means.Add("a ward spell");
        }

        if (s.CanParalyze)
        {
            means.Add("Paralyze");
        }

        if (s.HasHealPotion)
        {
            means.Add("heal potions");
        }

        if (s.HasBandage)
        {
            means.Add("bandages");
        }

        if (s.CanHealSpell)
        {
            means.Add("heal spells");
        }

        return means.Count == 0 ? null : string.Join(ListSeparator, means);
    }

    private static string StyleWord(CombatStyle style, bool caster, bool tankMage) =>
        style switch
        {
            CombatStyle.Mage => "mage",
            CombatStyle.Archer => caster ? "archer who casts" : "archer",
            _ => tankMage ? "warrior who casts" : "warrior"
        };

    private static string FoeStyleWord(CombatStyle style) =>
        style switch
        {
            CombatStyle.Mage => "casts spells",
            CombatStyle.Archer => "shoots from range",
            _ => "fights up close"
        };

    private static string DoingWord(bool casting, bool held) =>
        held ? "paralyzed" : casting ? "casting a spell" : "fighting";
}
