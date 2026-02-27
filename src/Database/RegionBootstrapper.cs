using System;
using TShockAPI;

namespace RegionExtension.Database
{
    internal sealed class RegionBootstrapper
    {
        private readonly DatabaseRepositoryFactory _databaseRepositoryFactory;

        public RegionBootstrapper(DatabaseRepositoryFactory databaseRepositoryFactory)
        {
            _databaseRepositoryFactory = databaseRepositoryFactory;
        }

        public RegionServices Initialize()
        {
            var connection = _databaseRepositoryFactory.CreateConnection(TShock.Config.Settings, TShock.SavePath);
            return new RegionServices
            {
                Connection = connection,
                InfoManager = new RegionInfoManager(connection),
                HistoryManager = new RegionHistoryManager(connection),
                DeletedRegions = new DeletedRegionsDB(connection)
            };
        }
    }
}
