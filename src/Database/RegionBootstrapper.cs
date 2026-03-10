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
            Func<System.Data.IDbConnection> writeConnectionFactory =
                () => _databaseRepositoryFactory.CreateConnection(TShock.Config.Settings, TShock.SavePath);
            return new RegionServices
            {
                Connection = connection,
                InfoManager = new RegionInfoManager(connection, writeConnectionFactory),
                HistoryManager = new RegionHistoryManager(connection, writeConnectionFactory),
                DeletedRegions = new DeletedRegionsDB(connection, writeConnectionFactory)
            };
        }
    }
}
