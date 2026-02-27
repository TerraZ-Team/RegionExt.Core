using System.Collections.Generic;
using TShockAPI.DB;

namespace RegionExtension.Infrastructure
{
    internal static class FastRegionLookup
    {
        public static int FindByUser(UserAccount user, List<FastRegion> fastRegions)
        {
            if (user == null || fastRegions == null)
                return -1;

            for (int i = 0; i < fastRegions.Count; i++)
                if (fastRegions[i]?.User == user)
                    return i;

            return -1;
        }
    }
}

