using System;
namespace HappyToy.V2
{
    // Counts are individual throws. A collected packet is always six throws.
    public static class SnapperStockRules
    {
        public const int ShotsPerPack = 6, MaximumPacks = 5, Capacity = ShotsPerPack * MaximumPacks;
        public const float InspectionSeconds = 6;
        public static bool CanCollect(int count) => count >= 0 && count <= Capacity - ShotsPerPack;
        public static int CurrentPack(int count) => count <= 0 ? 0 : (count - 1) % ShotsPerPack + 1;
        public static int ReservePacks(int count) => count <= 0 ? 0 : (count - 1) / ShotsPerPack;
        public static int Restore(int count, int version)
        {
            int maximum = version == 0 ? MaximumPacks : Capacity;
            if (version < 0 || version > 1 || count < 0 || count > maximum)
                throw new ArgumentException("Invalid snapper stock checkpoint");
            return version == 0 ? count * ShotsPerPack : count;
        }
    }
}
