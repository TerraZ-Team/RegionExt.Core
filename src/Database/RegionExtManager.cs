using RegionExtension.Commands;
using RegionExtension.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Data;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace RegionExtension.Database
{
    public class RegionExtManager
    {
        private readonly IDbConnection _tshockDatabase;
        private readonly RegionBootstrapper _bootstrapper;
        private readonly RegionDomainService _domainService;
        private readonly RegionRuntimeService _runtimeService;

        private RegionServices _services;
        private bool _fullyLoaded;

        public event EventHandler<RegionOperationEventArgs> RegionOperation;

        public RegionHistoryManager HistoryManager => _services?.HistoryManager;
        public DeletedRegionsDB DeletedRegions => _services?.DeletedRegions;
        public RegionInfoManager InfoManager => _services?.InfoManager;

        public RegionExtManager(IDbConnection db, DatabaseRepositoryFactory databaseRepositoryFactory = null, PluginContext context = null)
        {
            _tshockDatabase = db ?? throw new ArgumentNullException(nameof(db));
            _bootstrapper = new RegionBootstrapper(databaseRepositoryFactory ?? new DatabaseRepositoryFactory());
            _domainService = new RegionDomainService(
                _tshockDatabase,
                () => InfoManager,
                () => HistoryManager,
                () => DeletedRegions);
            _runtimeService = new RegionRuntimeService(context ?? new PluginContext());
        }

        public void PostInitialize(TerrariaPlugin plugin)
        {
            if (!InitializeDatabase())
                return;

            InfoManager?.PostInitialize();
        }

        private bool InitializeDatabase()
        {
            try
            {
                _services?.Connection?.Dispose();
                _services = _bootstrapper.Initialize();

                TShock.Log.Info("Info manager loaded.");
                TShock.Log.Info("History manager loaded.");
                TShock.Log.Info("Deleted region database loaded.");

                _fullyLoaded = true;
                TShock.Log.Info("Region extension manager fully loaded!");
                return true;
            }
            catch (Exception ex)
            {
                _fullyLoaded = false;
                TShock.Log.Error($"[RegionExt] Failed to initialize database layer: {ex}");
                return false;
            }
        }

        public void Dispose(TerrariaPlugin plugin)
        {
            _services?.Connection?.Dispose();
            _services = null;
            _fullyLoaded = false;
        }

        public bool RenameRegion(CommandArgsExtension args, Region region, string newName) =>
            ExecuteWithEvents(
                RegionOperationKind.Rename,
                args?.Player,
                region,
                () => _domainService.RenameRegion(args, region, newName),
                ("newName", newName));

        public bool MoveRegion(CommandArgsExtension args, Region region, int amount, Direction direction) =>
            ExecuteWithEvents(
                RegionOperationKind.Move,
                args?.Player,
                region,
                () => _domainService.MoveRegion(args, region, amount, direction),
                ("amount", amount),
                ("direction", direction));

        public bool AllowUser(CommandArgsExtension args, Region region, UserAccount account) =>
            ExecuteWithEvents(
                RegionOperationKind.AllowUser,
                args?.Player,
                region,
                () => _domainService.AllowUser(args, region, account),
                ("userId", account?.ID),
                ("userName", account?.Name));

        public bool RemoveUser(CommandArgsExtension args, Region region, UserAccount account) =>
            ExecuteWithEvents(
                RegionOperationKind.RemoveUser,
                args?.Player,
                region,
                () => _domainService.RemoveUser(args, region, account),
                ("userId", account?.ID),
                ("userName", account?.Name));

        public bool AllowGroup(CommandArgsExtension args, Region region, Group group) =>
            ExecuteWithEvents(
                RegionOperationKind.AllowGroup,
                args?.Player,
                region,
                () => _domainService.AllowGroup(args, region, group),
                ("group", group?.Name));

        public bool RemoveGroup(CommandArgsExtension args, Region region, Group group) =>
            ExecuteWithEvents(
                RegionOperationKind.RemoveGroup,
                args?.Player,
                region,
                () => _domainService.RemoveGroup(args, region, group),
                ("group", group?.Name));

        public bool SetZ(CommandArgsExtension args, Region region, int amount) =>
            ExecuteWithEvents(
                RegionOperationKind.SetZ,
                args?.Player,
                region,
                () => _domainService.SetZ(args, region, amount),
                ("z", amount));

        public bool Protect(CommandArgsExtension args, Region region, bool protect) =>
            ExecuteWithEvents(
                RegionOperationKind.Protect,
                args?.Player,
                region,
                () => _domainService.Protect(args, region, protect),
                ("protect", protect));

        public bool Resize(CommandArgsExtension args, Region region, int amount, int direction) =>
            ExecuteWithEvents(
                RegionOperationKind.Resize,
                args?.Player,
                region,
                () => _domainService.Resize(args, region, amount, direction),
                ("amount", amount),
                ("direction", direction));

        public bool ChangeOwner(CommandArgsExtension args, Region region, UserAccount account) =>
            ExecuteWithEvents(
                RegionOperationKind.ChangeOwner,
                args?.Player,
                region,
                () => _domainService.ChangeOwner(args, region, account),
                ("ownerId", account?.ID),
                ("ownerName", account?.Name));

        public bool DeleteRegion(CommandArgsExtension args, Region region) =>
            DeleteRegion(args?.Player, region);

        public bool DeleteRegion(TSPlayer user, Region region) =>
            ExecuteWithEvents(
                RegionOperationKind.Delete,
                user,
                region,
                () => _domainService.DeleteRegion(user, region));

        public bool DefineRegion(CommandArgsExtension args, Region region) =>
            DefineRegion(args?.Player, region);

        public bool DefineRegion(TSPlayer user, Region region) =>
            ExecuteWithEvents(
                RegionOperationKind.Define,
                user,
                region,
                () => _domainService.DefineRegion(user, region));

        public void Update()
        {
            if (!_fullyLoaded)
                return;
            _runtimeService.Update();
        }

        public void SendRequestNotify(TSPlayer player, IEnumerable<string> strings) =>
            _runtimeService.SendRequestNotify(player, strings);

        public List<string> GetRegionInfo(Region region) =>
            _domainService.GetRegionInfo(region);

        public List<string> GetRegionHistory(int count, Region region) =>
            _domainService.GetRegionHistory(count, region);

        public bool ClearAllowUsers(string regionName) =>
            _domainService.ClearAllowUsers(regionName);

        public void RegisterCommand(TSPlayer executor, Region region) =>
            _domainService.RegisterCommand(executor, region);

        internal void Reload(ReloadEventArgs e)
        {
            if (!_fullyLoaded)
            {
                TShock.Log.Warn("[RegionExt] Reload skipped: database layer is not initialized.");
                return;
            }

            _runtimeService.Reload(e);
        }

        private bool ExecuteWithEvents(
            RegionOperationKind operation,
            TSPlayer executor,
            Region region,
            Func<bool> operationDelegate,
            params (string key, object value)[] metadata)
        {
            if (!_fullyLoaded)
                return false;

            var beforeArgs = new RegionOperationEventArgs(
                operation,
                RegionOperationStage.Before,
                executor,
                region,
                BuildMetadata(metadata));
            RegionOperation?.Invoke(this, beforeArgs);
            if (beforeArgs.Cancel)
            {
                if (executor != null && !string.IsNullOrWhiteSpace(beforeArgs.CancelReason))
                    executor.SendErrorMessage(beforeArgs.CancelReason);
                return false;
            }

            var result = operationDelegate();
            if (!result)
                return false;

            var afterArgs = new RegionOperationEventArgs(
                operation,
                RegionOperationStage.After,
                executor,
                region,
                BuildMetadata(metadata));
            RegionOperation?.Invoke(this, afterArgs);
            return true;
        }

        private static Dictionary<string, object> BuildMetadata((string key, object value)[] metadata)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (metadata == null)
                return result;

            foreach (var entry in metadata)
            {
                if (string.IsNullOrWhiteSpace(entry.key))
                    continue;
                result[entry.key] = entry.value;
            }

            return result;
        }
    }
}
