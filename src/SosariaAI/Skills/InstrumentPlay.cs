using Server;
using Server.Items;

namespace SosariaAI.Skills;

/// <summary>
/// One try at a bard's song the way a player's client plays it: the skill check, a tune
/// played well or badly, and one use off the instrument. The provoke and peace jobs and the
/// songs a bard sings in a fight all play through here.
/// </summary>
public static class InstrumentPlay
{
    public static bool Try(Mobile bard, BaseInstrument instrument, SkillName skill, double practiceMin, double practiceMax)
    {
        var ok = bard.CheckSkill(skill, practiceMin, practiceMax);

        if (ok)
        {
            instrument.PlayInstrumentWell(bard);
        }
        else
        {
            instrument.PlayInstrumentBadly(bard);
        }

        instrument.ConsumeUse(bard);
        return ok;
    }
}
