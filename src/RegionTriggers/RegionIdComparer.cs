using System.Collections.Generic;
using TShockAPI.DB;

namespace RegionExtension.RegionTriggers
{
    internal sealed class RegionIdComparer : IEqualityComparer<Region>
    {
        public static RegionIdComparer Instance { get; } = new RegionIdComparer();

        public bool Equals(Region x, Region y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;
            return x.ID == y.ID;
        }

        public int GetHashCode(Region obj) => obj?.ID.GetHashCode() ?? 0;
    }
}
