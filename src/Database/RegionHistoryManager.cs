using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using RegionExtension.Database.Actions;
using TShockAPI;
using TShockAPI.DB;

namespace RegionExtension.Database
{
    public sealed class RegionHistoryManager : IDisposable
    {
        private readonly IDbConnection _database;
        private readonly BackgroundDbWriteQueue _writeQueue;
        private readonly Dictionary<int, Stack<ActionInfo>> _redoActions = new();

        private readonly SqlTable _table =
            new("RegionHistory",
                 new SqlColumn(TableHistoryInfo.Id.ToString(), MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                 new SqlColumn(TableHistoryInfo.RegionId.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableHistoryInfo.UserId.ToString(), MySqlDbType.Int32),
                 new SqlColumn(TableHistoryInfo.ActionName.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableHistoryInfo.Args.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableHistoryInfo.UndoArgs.ToString(), MySqlDbType.Text),
                 new SqlColumn(TableHistoryInfo.DateTime.ToString(), MySqlDbType.Int64));

        public RegionHistoryManager(IDbConnection db, Func<IDbConnection> writeConnectionFactory)
        {
            _database = db;
            _writeQueue = new BackgroundDbWriteQueue(
                writeConnectionFactory ?? throw new ArgumentNullException(nameof(writeConnectionFactory)),
                "RegionExt.Core.History");
            InitializeTable();
        }

        public void InitializeTable()
        {
            var creator = new SqlTableCreator(_database, QueryBuilderFactory.Create(_database));
            creator.EnsureTableStructure(_table);
        }

        public void SaveAction(IAction action, Region region, UserAccount user, DateTime dateTime, bool clearRedo = true)
        {
            var name = action.Name;
            var args = action.GetArgsString();
            var undoArgs = action.GetUndoArgsString();
            var regionId = region.ID;
            var userId = user?.ID ?? 0;

            if (_redoActions.ContainsKey(regionId) && clearRedo)
                _redoActions.Remove(regionId);

            _writeQueue.TryEnqueue(connection =>
            {
                var variablesString = string.Join(", ", _table.Columns.Select(c => c.Name).Where(s => s != TableHistoryInfo.Id.ToString()));
                connection.Query(
                    $"INSERT INTO {_table.Name} ({variablesString}) VALUES (@0, @1, @2, @3, @4, @5);",
                    regionId,
                    userId,
                    name,
                    args,
                    undoArgs,
                    DateTimeCodec.ToUnixMilliseconds(dateTime));
            });
        }

        public void SaveAction(IAction action, Region region, UserAccount user)
        {
            SaveAction(action, region, user, DateTime.UtcNow);
        }

        public bool Undo(int count, int regionId)
        {
            _writeQueue.Flush();

            var actions = LoadActions(regionId);
            if (actions == null)
                return false;

            foreach (var action in actions.OrderBy(a => a.Date).Reverse())
            {
                var undoAction = action.Action.GetUndoAction(action.UndoStr);
                if (!_redoActions.ContainsKey(action.RegionId))
                    _redoActions.Add(action.RegionId, new Stack<ActionInfo>(10));
                _redoActions[action.RegionId].Push(new ActionInfo(action.Id, action.Action, action.RegionId, action.UserId, action.Date, action.UndoStr));
                undoAction.Do();
                _database.Query($"DELETE FROM {_table.Name} WHERE Id=@0", action.Id);
                count--;
                if (count < 1)
                    break;
            }

            return true;
        }

        public List<string> GetActionsInfo(int count, int regionId)
        {
            _writeQueue.Flush();

            var actions = LoadActions(regionId);
            if (actions == null)
                return null;

            return actions.OrderBy(a => a.Date)
                          .Reverse()
                          .Take(count)
                          .Select(a =>
                          {
                              var user = a.UserId == 0 ? null : TShock.UserAccounts.GetUserAccountByID(a.UserId);
                              var userName = user?.Name ?? "Server";
                              return string.Join(' ',
                                  a.Date.ToString(Utils.DateFormat),
                                  userName,
                                  string.Join(' ', a.Action.GetInfoString()));
                          })
                          .ToList();
        }

        public void Redo(int count, int regionId)
        {
            if (!_redoActions.ContainsKey(regionId))
                return;

            while (count > 0 && _redoActions[regionId].Count > 0)
            {
                count--;
                var actionInfo = _redoActions[regionId].Pop();
                SaveAction(
                    actionInfo.Action,
                    TShock.Regions.GetRegionByID(regionId),
                    TShock.UserAccounts.GetUserAccountByID(actionInfo.UserId),
                    actionInfo.Date,
                    false);
                actionInfo.Action.Do();
            }
        }

        public void Dispose()
        {
            _writeQueue.Flush();
            _writeQueue.Dispose();
        }

        private List<ActionInfo> LoadActions(int regionId)
        {
            return DbSafe.Read("Load history", () =>
            {
                var actions = new List<ActionInfo>();
                using var reader = _database.QueryReader($"SELECT * FROM {_table.Name} WHERE RegionId=@0", regionId);
                while (reader.Read())
                {
                    var id = reader.Get<int>(_table.Columns[0].Name);
                    var loadedRegionId = reader.Get<int>(_table.Columns[1].Name);
                    var userId = reader.Get<int>(_table.Columns[2].Name);
                    var actionName = reader.Get<string>(_table.Columns[3].Name);
                    var args = reader.Get<string>(_table.Columns[4].Name);
                    var undoArgs = reader.Get<string>(_table.Columns[5].Name);
                    var dateTime = DateTimeCodec.FromUnixMilliseconds(reader.Get<long>(_table.Columns[6].Name));
                    var action = ActionFactory.GetActionByName(actionName, args);
                    if (action != null)
                        actions.Add(new ActionInfo(id, action, loadedRegionId, userId, dateTime, undoArgs));
                }

                return actions;
            });
        }

        private enum TableHistoryInfo
        {
            Id,
            RegionId,
            UserId,
            ActionName,
            Args,
            UndoArgs,
            DateTime
        }
    }

    public sealed class ActionDBInfo
    {
        public int RegionId { get; set; }
        public int UserId { get; set; }
        public string ActionName { get; set; }
        public string Args { get; set; }
        public string UndoArgs { get; set; }
        public DateTime DateTime { get; set; }
    }

    public sealed class ActionInfo
    {
        public ActionInfo(int id, IAction action, int regionId, int userId, DateTime date, string undoStr)
        {
            Id = id;
            Action = action;
            RegionId = regionId;
            UserId = userId;
            Date = date;
            UndoStr = undoStr;
        }

        public int Id { get; }
        public IAction Action { get; }
        public int RegionId { get; }
        public int UserId { get; }
        public string UndoStr { get; }
        public DateTime Date { get; }
    }
}
