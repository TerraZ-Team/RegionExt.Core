using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace RegionExtension.Database
{
    public sealed class RegionInfoManager : IDisposable
    {
        private readonly IDbConnection _database;
        private readonly CoalescingBackgroundDbWriteQueue<int> _writeQueue;
        private readonly Dictionary<int, RegionExtensionInfo> _regionsInfoById = new();

        private readonly SqlTable _table =
            new("ExtendedRegions",
                 new SqlColumn(TableInfo.Id.ToString(), MySqlDbType.Int32) { Unique = true, NotNull = true },
                 new SqlColumn(TableInfo.WorldId.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.DateCreation.ToString(), MySqlDbType.Int64),
                 new SqlColumn(TableInfo.LastUser.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.LastUpdate.ToString(), MySqlDbType.Int64),
                 new SqlColumn(TableInfo.LastActivity.ToString(), MySqlDbType.Int64));

        public IReadOnlyCollection<RegionExtensionInfo> RegionsInfo => _regionsInfoById.Values;

        public RegionInfoManager(IDbConnection db, Func<IDbConnection> writeConnectionFactory)
        {
            _database = db;
            _writeQueue = new CoalescingBackgroundDbWriteQueue<int>(
                writeConnectionFactory ?? throw new ArgumentNullException(nameof(writeConnectionFactory)),
                "RegionExt.Core.Info");
            InitializeTable();
        }

        public void InitializeTable()
        {
            var creator = new SqlTableCreator(_database, QueryBuilderFactory.Create(_database));
            creator.EnsureTableStructure(_table);
        }

        public void PostInitialize()
        {
            LoadRegions();
        }

        public bool AddNewRegion(int id, int userId)
        {
            if (_regionsInfoById.ContainsKey(id))
                return true;

            var info = new RegionExtensionInfo(id, userId);
            _regionsInfoById[id] = info;
            return ScheduleUpsert(info);
        }

        public bool RemoveRegion(int id)
        {
            if (!_regionsInfoById.Remove(id))
                return false;

            return _writeQueue.TryEnqueueOrReplace(id, connection =>
                connection.Query($"DELETE FROM {_table.Name} WHERE Id=@0", id));
        }

        public bool UpdateLastUser(int id, int userId)
        {
            if (!TryGetRegionInfo(id, out var info))
                return false;

            info.LastUserId = userId;
            return ScheduleUpsert(info);
        }

        public bool UpdateLastUser(Region region, UserAccount user) =>
            UpdateLastUser(region.ID, user.ID);

        public bool UpdateLastUpdate(int id, DateTime time)
        {
            if (!TryGetRegionInfo(id, out var info))
                return false;

            info.LastUpdate = time;
            return ScheduleUpsert(info);
        }

        public bool UpdateLastUpdate(Region region, DateTime time) =>
            UpdateLastUpdate(region.ID, time);

        public bool UpdateLastActivity(int id, DateTime time)
        {
            if (!TryGetRegionInfo(id, out var info))
                return false;

            info.LastActivity = time;
            return ScheduleUpsert(info);
        }

        public bool UpdateLastActivity(Region region, DateTime time) =>
            UpdateLastActivity(region.ID, time);

        public bool TryGetRegionInfo(int id, out RegionExtensionInfo info) =>
            _regionsInfoById.TryGetValue(id, out info);

        public List<string> GetRegionInfo(int id, bool insertDefaultInfo = true)
        {
            var region = TShock.Regions.GetRegionByID(id);
            if (region == null)
                return null;

            var lines = new List<string>
            {
                string.Format("X: {0}; Y: {1}; W: {2}; H: {3}, Z: {4}", region.Area.X, region.Area.Y, region.Area.Width, region.Area.Height, region.Z),
                string.Concat("Owner: ", region.Owner),
                string.Concat("Protected: ", region.DisableBuild.ToString())
            };

            if (region.AllowedIDs.Count > 0)
            {
                var sharedUsersSelector = region.AllowedIDs.Select(userId =>
                {
                    var user = TShock.UserAccounts.GetUserAccountByID(userId);
                    return user != null ? user.Name : string.Concat("{ID: ", userId, "}");
                });
                var extraLines = PaginationTools.BuildLinesFromTerms(sharedUsersSelector.Distinct());
                extraLines[0] = "Shared with: " + extraLines[0];
                lines.AddRange(extraLines);
            }
            else
            {
                lines.Add("Region is not shared with any users.");
            }

            if (region.AllowedGroups.Count > 0)
            {
                var extraLines = PaginationTools.BuildLinesFromTerms(region.AllowedGroups.Distinct());
                extraLines[0] = "Shared with groups: " + extraLines[0];
                lines.AddRange(extraLines);
            }
            else
            {
                lines.Add("Region is not shared with any groups.");
            }

            if (!TryGetRegionInfo(id, out var extInfo) && insertDefaultInfo)
            {
                AddNewRegion(id, GetOwnerIdOrDefault(region.Owner));
                TryGetRegionInfo(id, out extInfo);
            }

            if (extInfo != null)
            {
                var user = TShock.UserAccounts.GetUserAccountByID(extInfo.LastUserId);
                var userName = user == null ? "N/A" : user.Name;
                lines.Add(string.Concat("Last user: ", userName));
                lines.Add(string.Concat("Last update: ", extInfo.LastUpdate.ToString(Utils.DateFormat)));
                lines.Add(string.Concat("Last activity: ", extInfo.LastActivity.ToString(Utils.DateFormat)));
                lines.Add(string.Concat("Date creation: ", extInfo.DateCreation.ToString(Utils.DateFormat)));
            }

            return lines;
        }

        public void Dispose() =>
            _writeQueue.Dispose();

        private void LoadRegions()
        {
            DbSafe.Execute("Load region info", () =>
            {
                _regionsInfoById.Clear();
                using (var reader = _database.QueryReader($"SELECT * FROM {_table.Name} WHERE WorldId=@0", Main.worldID.ToString()))
                {
                    while (reader.Read())
                    {
                        var info = new RegionExtensionInfo(
                            reader.Get<int>(TableInfo.Id.ToString()),
                            reader.Get<int>(TableInfo.LastUser.ToString()),
                            DateTimeCodec.FromUnixMilliseconds(reader.Get<long>(TableInfo.DateCreation.ToString())),
                            DateTimeCodec.FromUnixMilliseconds(reader.Get<long>(TableInfo.LastUpdate.ToString())),
                            DateTimeCodec.FromUnixMilliseconds(reader.Get<long>(TableInfo.LastActivity.ToString())));
                        _regionsInfoById[info.Id] = info;
                    }
                }

                foreach (var region in TShock.Regions.Regions)
                {
                    if (!_regionsInfoById.ContainsKey(region.ID))
                        AddNewRegion(region.ID, GetOwnerIdOrDefault(region.Owner));
                }
            });
        }

        private bool ScheduleUpsert(RegionExtensionInfo info)
        {
            var snapshot = new RegionExtensionInfo(
                info.Id,
                info.LastUserId,
                info.DateCreation,
                info.LastUpdate,
                info.LastActivity);

            return _writeQueue.TryEnqueueOrReplace(snapshot.Id, connection =>
            {
                var affected = connection.Query(
                    $"UPDATE {_table.Name} SET WorldId=@1, DateCreation=@2, LastUser=@3, LastUpdate=@4, LastActivity=@5 WHERE Id=@0",
                    snapshot.Id,
                    Main.worldID,
                    DateTimeCodec.ToUnixMilliseconds(snapshot.DateCreation),
                    snapshot.LastUserId,
                    DateTimeCodec.ToUnixMilliseconds(snapshot.LastUpdate),
                    DateTimeCodec.ToUnixMilliseconds(snapshot.LastActivity));

                if (affected > 0)
                    return;

                connection.Query(
                    $"INSERT INTO {_table.Name} (Id, WorldId, DateCreation, LastUser, LastUpdate, LastActivity) VALUES (@0, @1, @2, @3, @4, @5);",
                    snapshot.Id,
                    Main.worldID,
                    DateTimeCodec.ToUnixMilliseconds(snapshot.DateCreation),
                    snapshot.LastUserId,
                    DateTimeCodec.ToUnixMilliseconds(snapshot.LastUpdate),
                    DateTimeCodec.ToUnixMilliseconds(snapshot.LastActivity));
            });
        }

        private enum TableInfo
        {
            Id,
            WorldId,
            DateCreation,
            LastUser,
            LastUpdate,
            LastActivity
        }

        private static int GetOwnerIdOrDefault(string ownerName)
        {
            var owner = TShock.UserAccounts.GetUserAccountByName(ownerName);
            return owner?.ID ?? 0;
        }
    }

    public sealed class RegionExtensionInfo
    {
        public RegionExtensionInfo(int id, int lastUser)
            : this(id, lastUser, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow)
        {
        }

        public RegionExtensionInfo(int id, int lastUserId, DateTime dateCreation, DateTime lastUpdate, DateTime lastActivity)
        {
            Id = id;
            DateCreation = dateCreation;
            LastUserId = lastUserId;
            LastUpdate = lastUpdate;
            LastActivity = lastActivity;
        }

        public int Id { get; set; }
        public int LastUserId { get; set; }
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }
}
