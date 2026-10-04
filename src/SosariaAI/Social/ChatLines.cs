using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Social;

/// <summary>
/// Bank-floor shouts: a WTS or WTB line built from the real goods and price, which the crowd
/// may answer. The lines live in the talk library (<see cref="TalkCategory.Wts"/>,
/// <see cref="TalkCategory.Wtb"/>).
/// </summary>
public static class ChatLines
{
    /// <summary>Shouts the offer or the want. <paramref name="price"/> is only said when selling.</summary>
    public static void Shout(SosariaCharacter shouter, bool selling, string item, string price)
    {
        var slots = new TalkSlots { Item = item, Price = selling ? price : null };

        if (Talk.Say(shouter, selling ? TalkCategory.Wts : TalkCategory.Wtb, slots))
        {
            Scenes.ShoutAnswer(shouter, selling, item, price);
        }
    }
}
