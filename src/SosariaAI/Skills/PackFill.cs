namespace SosariaAI.Skills;

public static class PackFill
{
    public static bool IsAtOrAboveFraction(int currentWeight, int maxWeight, double fraction)
    {
        if (fraction <= 0 || maxWeight <= 0)
        {
            return true;
        }

        return currentWeight >= maxWeight * fraction;
    }
}
