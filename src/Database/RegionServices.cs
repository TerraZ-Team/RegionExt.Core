using System.Data;

namespace RegionExtension.Database
{
    internal sealed class RegionServices : System.IDisposable
    {
        public IDbConnection Connection { get; init; }
        public RegionInfoManager InfoManager { get; init; }
        public RegionHistoryManager HistoryManager { get; init; }
        public DeletedRegionsDB DeletedRegions { get; init; }

        public void Dispose()
        {
            DeletedRegions?.Dispose();
            HistoryManager?.Dispose();
            InfoManager?.Dispose();
            Connection?.Dispose();
        }
    }
}
