using System;

namespace HappyToy.V2
{
    public static class TalismanStockRules
    {
        public const int FragmentsPerTalisman = 5;

        public static bool TryCollectFragment(int fragments, int talismans,
            out int nextFragments, out int nextTalismans)
        {
            Validate(fragments, talismans);
            nextFragments = fragments;
            nextTalismans = talismans;
            if (fragments == FragmentsPerTalisman - 1 && talismans == int.MaxValue)
                return false;
            nextFragments++;
            if (nextFragments == FragmentsPerTalisman)
            {
                nextFragments = 0;
                nextTalismans++;
            }
            return true;
        }

        public static void Validate(int fragments, int talismans)
        {
            if (fragments < 0 || fragments >= FragmentsPerTalisman || talismans < 0)
                throw new ArgumentException("Invalid talisman stock checkpoint");
        }
    }
}
