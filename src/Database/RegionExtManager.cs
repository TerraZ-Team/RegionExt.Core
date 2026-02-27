using RegionExtension.Commands;
using RegionExtension.Commands.Parameters;
using RegionExtension.RegionTriggers;
using RegionExtension.RegionTriggers.Conditions;
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
        private readonly PluginContext _context;
        private readonly RegionBootstrapper _bootstrapper;
        private readonly RegionDomainService _domainService;
        private readonly RegionRuntimeService _runtimeService;

        private RegionServices _services;
        private RegionRequestManager _requestManager;
        private TriggerManager _triggerManager;
        private PropertyManager _propertyManager;
        private bool _fullyLoaded;

        public RegionHistoryManager HistoryManager => _services?.HistoryManager;
        public DeletedRegionsDB DeletedRegions => _services?.DeletedRegions;
        public RegionInfoManager InfoManager => _services?.InfoManager;
        public RegionRequestManager RegionRequestManager => _requestManager;
        public TriggerManager TriggerManager => _triggerManager;
        public PropertyManager PropertyManager => _propertyManager;

        public RegionExtManager(IDbConnection db, DatabaseRepositoryFactory databaseRepositoryFactory = null, PluginContext context = null)
        {
            _tshockDatabase = db ?? throw new ArgumentNullException(nameof(db));
            _context = context ?? new PluginContext();
            _bootstrapper = new RegionBootstrapper(databaseRepositoryFactory ?? new DatabaseRepositoryFactory());
            _domainService = new RegionDomainService(
                _tshockDatabase,
                () => InfoManager,
                () => HistoryManager,
                () => DeletedRegions,
                () => RegionRequestManager,
                () => TriggerManager,
                () => PropertyManager);
            _runtimeService = new RegionRuntimeService(
                _context,
                () => RegionRequestManager,
                () => TriggerManager,
                () => PropertyManager,
                _domainService.RemoveRequest);
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
                _requestManager = null;
                _triggerManager = null;
                _propertyManager = null;

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

        public bool EnableRequests()
        {
            if (!_fullyLoaded || _services?.Connection == null)
                return false;

            _requestManager ??= new RegionRequestManager(_services.Connection);
            return true;
        }

        public void DisableRequests()
        {
            _requestManager = null;
        }

        public bool EnableTriggers(TerrariaPlugin plugin)
        {
            if (plugin == null || !_fullyLoaded || _services?.Connection == null)
                return false;

            var shouldInitialize = _triggerManager == null || _propertyManager == null;
            _triggerManager ??= new TriggerManager(_services.Connection, _context.TriggerIgnores);
            _propertyManager ??= new PropertyManager(_services.Connection, plugin, _context);
            if (shouldInitialize)
                DelayManager.Initialize(plugin);
            return true;
        }

        public void DisableTriggers(TerrariaPlugin plugin)
        {
            var shouldDisposeDelayManager = _triggerManager != null || _propertyManager != null;
            if (plugin != null)
            {
                _propertyManager?.Dispose(plugin);
                if (shouldDisposeDelayManager)
                    DelayManager.Dispose(plugin);
            }

            _propertyManager = null;
            _triggerManager = null;
        }

        public void Dispose(TerrariaPlugin plugin)
        {
            DisableRequests();
            DisableTriggers(plugin);
            _services?.Connection?.Dispose();
            _services = null;
            _fullyLoaded = false;
        }

        public bool RenameRegion(CommandArgsExtension args, Region region, string newName) =>
            _domainService.RenameRegion(args, region, newName);

        public bool MoveRegion(CommandArgsExtension args, Region region, int amount, Direction direction) =>
            _domainService.MoveRegion(args, region, amount, direction);

        public bool AllowUser(CommandArgsExtension args, Region region, UserAccount account) =>
            _domainService.AllowUser(args, region, account);

        public bool RemoveUser(CommandArgsExtension args, Region region, UserAccount account) =>
            _domainService.RemoveUser(args, region, account);

        public bool AllowGroup(CommandArgsExtension args, Region region, Group group) =>
            _domainService.AllowGroup(args, region, group);

        public bool RemoveGroup(CommandArgsExtension args, Region region, Group group) =>
            _domainService.RemoveGroup(args, region, group);

        public bool SetZ(CommandArgsExtension args, Region region, int amount) =>
            _domainService.SetZ(args, region, amount);

        public bool Protect(CommandArgsExtension args, Region region, bool protect) =>
            _domainService.Protect(args, region, protect);

        public bool Resize(CommandArgsExtension args, Region region, int amount, int direction) =>
            _domainService.Resize(args, region, amount, direction);

        public bool ChangeOwner(CommandArgsExtension args, Region region, UserAccount account) =>
            _domainService.ChangeOwner(args, region, account);

        public bool DeleteRegion(CommandArgsExtension args, Region region) =>
            DeleteRegion(args.Player, region);

        public bool DeleteRegion(TSPlayer user, Region region) =>
            _domainService.DeleteRegion(user, region);

        public bool DefineRegion(CommandArgsExtension args, Region region) =>
            DefineRegion(args.Player, region);

        public bool DefineRegion(TSPlayer user, Region region) =>
            _domainService.DefineRegion(user, region);

        public bool CreateRequest(Region region, TSPlayer user) =>
            _domainService.CreateRequest(region, user);

        public bool ApproveRequest(UserAccount user, int regionId) =>
            _domainService.ApproveRequest(user, regionId);

        public bool ApproveRequest(UserAccount user, Region region) =>
            _domainService.ApproveRequest(user, region);

        public bool ApproveRequest(UserAccount user, Request request) =>
            _domainService.ApproveRequest(user, request);

        public bool DenyRequest(UserAccount user, int regionId) =>
            _domainService.DenyRequest(user, regionId);

        public bool DenyRequest(UserAccount user, Region region) =>
            _domainService.DenyRequest(user, region);

        public bool DenyRequest(UserAccount user, Request request) =>
            _domainService.DenyRequest(user, request);

        public bool RemoveRequest(Region region, UserAccount user, bool approved) =>
            _domainService.RemoveRequest(region, user, approved);

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
    }
}
