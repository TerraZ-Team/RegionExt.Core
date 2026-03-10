using Terraria;
using MonoMod;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace RegionExtension.Database
{
    public sealed class DeletedRegionsDB : IDisposable
    {
        private readonly IDbConnection _database;
        private readonly BackgroundDbWriteQueue _writeQueue;

        private List<DeletedInfo> _deletedInfo = new List<DeletedInfo>();

        private SqlTable _table =
            new SqlTable("DeletedRegions",
                 new SqlColumn(TableInfo.RegionId.ToString(), MySqlDbType.Int32) { NotNull = true },
                 new SqlColumn(TableInfo.DeleterId.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.WorldId.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableInfo.RegionName.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableInfo.X.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.Y.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.Width.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.Height.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.UserIds.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableInfo.Protected.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.Groups.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableInfo.Owner.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableInfo.Z.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableInfo.CreationDate.ToString(), MySqlDbType.Int64),
                 new SqlColumn(TableInfo.DeletionDate.ToString(), MySqlDbType.Int64)
                 );

        public DeletedRegionsDB(IDbConnection db, Func<IDbConnection> writeConnectionFactory)
        {
            _database = db;
            _writeQueue = new BackgroundDbWriteQueue(
                writeConnectionFactory ?? throw new ArgumentNullException(nameof(writeConnectionFactory)),
                "RegionExt.Core.DeletedRegions");
            InitializeTable();
        }

        public void InitializeTable()
        {
            var creator = new SqlTableCreator(_database, QueryBuilderFactory.Create(_database));
            creator.EnsureTableStructure(_table);
            LoadRegions();
        }

        public bool RegisterDeletedRegion(Region region, UserAccount userDeleter, RegionExtensionInfo info)
        {
            var deleterId = userDeleter?.ID ?? 0;
            var deleterName = userDeleter?.Name ?? "Server";
            while (_deletedInfo.Any(r => r.RegionExt.Region.ID == region.ID))
                region.ID++;

            var deletionDate = DateTime.UtcNow;
            var snapshot = new DeletedRegionRecord(
                region.ID,
                deleterId,
                region.WorldID,
                region.Name,
                region.Area.X,
                region.Area.Y,
                region.Area.Width,
                region.Area.Height,
                string.Join(',', region.AllowedIDs),
                region.DisableBuild ? 1 : 0,
                string.Join(' ', region.AllowedGroups),
                region.Owner,
                region.Z,
                DateTimeCodec.ToUnixMilliseconds(info.DateCreation),
                DateTimeCodec.ToUnixMilliseconds(deletionDate));

            _deletedInfo.Add(new DeletedInfo(new RegionExtended() { Region = region, ExtensionInfo = info }, deletionDate, deleterName));
            return _writeQueue.TryEnqueue(connection =>
            {
                string query = $"INSERT INTO {_table.Name} (RegionId, DeleterId, WorldId, RegionName, X, Y, Width, Height, UserIds, Protected, `Groups`, Owner, Z, CreationDate, DeletionDate) VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14);";
                connection.Query(query,
                    snapshot.RegionId,
                    snapshot.DeleterId,
                    snapshot.WorldId,
                    snapshot.RegionName,
                    snapshot.X,
                    snapshot.Y,
                    snapshot.Width,
                    snapshot.Height,
                    snapshot.UserIds,
                    snapshot.Protected,
                    snapshot.Groups,
                    snapshot.Owner,
                    snapshot.Z,
                    snapshot.CreationDate,
                    snapshot.DeletionDate);
            });
        }

        public bool LoadRegions()
        {
            return DbSafe.Execute("Load deleted regions", () =>
            {
                using (var reader = _database.QueryReader($"SELECT * FROM {_table.Name} WHERE {TableInfo.WorldId}=@0", Main.worldID.ToString()))
                {
                    while (reader.Read())
                    {
                        var info = DeletedInfo.ReadFromDB(reader);
                        _deletedInfo.Add(info);
                    }
                }
                _deletedInfo = _deletedInfo.OrderBy(r => r.DeletionDate)
                                           .Reverse().ToList();
                RemoveMoreThanMaxRegions();
                return true;
            });
        }

        public bool RemoveMoreThanMaxRegions()
        {
            var max = 64;
            return DbSafe.Execute("Trim deleted regions", () =>
            {
                while ( _deletedInfo.Count > max )
                {
                    var lastReg = _deletedInfo.Last();
                    _deletedInfo.Remove(lastReg);
                    _database.Query($"DELETE FROM {_table.Name} WHERE RegionId=@0", lastReg.RegionExt.Region.ID);
                }
                return true;
            });
        }

        public List<string> GetRegionsInfo() => 
            _deletedInfo.OrderBy(r => r.DeletionDate)
                        .Reverse()
                        .Select(r => string.Join(' ', r.DeletionDate.ToString(Utils.DateFormat), r.RegionName, r.DeleterUser))
                        .ToList();

        public bool RemoveRegionFromDeleted(int regionId)
        {
            _deletedInfo.RemoveAll(r => r.RegionExt.Region.ID == regionId);
            return _writeQueue.TryEnqueue(connection =>
                connection.Query($"DELETE FROM {_table.Name} WHERE RegionId=@0", regionId));
        }

        public RegionExtended GetRegionByName(string regionName)
        {
            var reg = _deletedInfo.FirstOrDefault(r => r.RegionName == regionName);
            if (reg == null)
                return null;
            return reg.RegionExt;
        }

        public List<RegionExtended> GetRegionsByUser(UserAccount user) =>
            _deletedInfo.Where(r => r.DeleterUser == user.Name)
                        .Select(r => r.RegionExt)
                        .ToList();

        public void Dispose()
        {
            _writeQueue.Flush();
            _writeQueue.Dispose();
        }

        public enum TableInfo
        {
            RegionId,
            DeleterId,
            WorldId,
            RegionName,
            X,
            Y,
            Width,
            Height,
            UserIds,
            Protected,
            Groups,
            Owner,
            Z,
            CreationDate,
            DeletionDate
        }
    }

    internal readonly record struct DeletedRegionRecord(
        int RegionId,
        int DeleterId,
        string WorldId,
        string RegionName,
        int X,
        int Y,
        int Width,
        int Height,
        string UserIds,
        int Protected,
        string Groups,
        string Owner,
        int Z,
        long CreationDate,
        long DeletionDate);

    public class DeletedInfo
    {
        public DeletedInfo(RegionExtended region, DateTime deletionDate, string deleterUser)
        {
            RegionExt = region;
            DeletionDate = deletionDate;
            DeleterUser = deleterUser;
        }

        public RegionExtended RegionExt { get; set; }
        public string RegionName { get => RegionExt.Region.Name; }
        public DateTime DeletionDate { get; set; }
        public string DeleterUser{ get; set; }

        public static DeletedInfo ReadFromDB(QueryResult reader)
        {
            int id = reader.Get<int>(DeletedRegionsDB.TableInfo.RegionId.ToString());
            var worldId = reader.Get<string>(DeletedRegionsDB.TableInfo.WorldId.ToString());
            var name = reader.Get<string>(DeletedRegionsDB.TableInfo.RegionName.ToString());
            var area = new Microsoft.Xna.Framework.Rectangle(
                        reader.Get<int>(DeletedRegionsDB.TableInfo.X.ToString()),
                        reader.Get<int>(DeletedRegionsDB.TableInfo.Y.ToString()),
                        reader.Get<int>(DeletedRegionsDB.TableInfo.Width.ToString()),
                        reader.Get<int>(DeletedRegionsDB.TableInfo.Height.ToString())
                        );
            var allowIdString = reader.Get<string>(DeletedRegionsDB.TableInfo.UserIds.ToString()).Split(',');
            var allowIds = new List<int>();
            foreach (var str in allowIdString)
            {
                int n = 0;
                if (int.TryParse(str, out n))
                    allowIds.Add(n);
            }
            var disableBuild = reader.Get<int>(DeletedRegionsDB.TableInfo.Protected.ToString()) == 1 ? true : false;
            var allowedGroups = reader.Get<string>(DeletedRegionsDB.TableInfo.Groups.ToString()).Split(' ').ToList();
            var owner = reader.Get<string>(DeletedRegionsDB.TableInfo.Owner.ToString());
            var z = reader.Get<int>(DeletedRegionsDB.TableInfo.Z.ToString());
            var deletionTime = DateTimeCodec.FromUnixMilliseconds(reader.Get<long>(DeletedRegionsDB.TableInfo.DeletionDate.ToString()));
            var userid = reader.Get<int>(DeletedRegionsDB.TableInfo.DeleterId.ToString());
            var user = TShock.UserAccounts.GetUserAccountByID(userid);
            var username = user == null ? "Server" : user.Name;
            var ownerAccount = TShock.UserAccounts.GetUserAccountByName(owner);
            var ownerId = ownerAccount?.ID ?? 0;
            return new DeletedInfo(new RegionExtended()
            {
                Region = new Region()
                {
                    ID = id,
                    WorldID = worldId,
                    Name = name,
                    Area = area,
                    AllowedIDs = allowIds,
                    DisableBuild = disableBuild,
                    AllowedGroups = allowedGroups,
                    Owner = owner,
                    Z = z
                },
                ExtensionInfo = new RegionExtensionInfo(
                    id,
                    ownerId,
                    DateTimeCodec.FromUnixMilliseconds(reader.Get<long>(DeletedRegionsDB.TableInfo.CreationDate.ToString())),
                    DateTime.UtcNow,
                    DateTime.UtcNow
                )
            }, deletionTime, username);
        }
    }
}

