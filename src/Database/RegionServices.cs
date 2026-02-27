using System.Data;

namespace RegionExtension.Database
{
    internal sealed class RegionServices
    {
        public IDbConnection Connection { get; init; }
        public RegionInfoManager InfoManager { get; init; }
        public RegionHistoryManager HistoryManager { get; init; }
        public DeletedRegionsDB DeletedRegions { get; init; }
    }
}
