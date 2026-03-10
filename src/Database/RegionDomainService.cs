using RegionExtension.Commands;
using RegionExtension.Commands.Parameters;
using RegionExtension.Database.Actions;
using RegionExtension.Database.EventsArgs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace RegionExtension.Database
{
    internal sealed class RegionDomainService
    {
        private readonly IDbConnection _tshockDatabase;
        private readonly Func<RegionInfoManager> _infoManagerProvider;
        private readonly Func<RegionHistoryManager> _historyManagerProvider;
        private readonly Func<DeletedRegionsDB> _deletedRegionsProvider;

        public RegionDomainService(
            IDbConnection tshockDatabase,
            Func<RegionInfoManager> infoManagerProvider,
            Func<RegionHistoryManager> historyManagerProvider,
            Func<DeletedRegionsDB> deletedRegionsProvider)
        {
            _tshockDatabase = tshockDatabase;
            _infoManagerProvider = infoManagerProvider;
            _historyManagerProvider = historyManagerProvider;
            _deletedRegionsProvider = deletedRegionsProvider;
        }

        public bool RenameRegion(CommandArgsExtension args, Region region, string newName)
        {
            RegisterAction(new Rename(new RenameArgs(args.Player, region, newName)), args.Player, region);
            return TShock.Regions.RenameRegion(region.Name, newName);
        }

        public bool MoveRegion(CommandArgsExtension args, Region region, int amount, Direction direction)
        {
            RegisterAction(new Move(new MoveArgs(args.Player, region, amount, direction)), args.Player, region);
            var newPos = direction.GetNewPosition(region.Area.X, region.Area.Y, amount);
            return TShock.Regions.PositionRegion(region.Name, newPos.x, newPos.y, region.Area.Width, region.Area.Height);
        }

        public bool AllowUser(CommandArgsExtension args, Region region, UserAccount account)
        {
            RegisterAction(new Allow(new AllowArgs(args.Player, region, account)), args.Player, region);
            return TShock.Regions.AddNewUser(region.Name, account.Name);
        }

        public bool RemoveUser(CommandArgsExtension args, Region region, UserAccount account)
        {
            RegisterAction(new Remove(new RemoveArgs(args.Player, region, account)), args.Player, region);
            return TShock.Regions.RemoveUser(region.Name, account.Name);
        }

        public bool AllowGroup(CommandArgsExtension args, Region region, Group group)
        {
            RegisterAction(new AllowGroup(new AllowGroupArgs(args.Player, region, group)), args.Player, region);
            return TShock.Regions.AllowGroup(region.Name, group.Name);
        }

        public bool RemoveGroup(CommandArgsExtension args, Region region, Group group)
        {
            RegisterAction(new RemoveGroup(new RemoveGroupArgs(args.Player, region, group)), args.Player, region);
            return TShock.Regions.RemoveGroup(region.Name, group.Name);
        }

        public bool SetZ(CommandArgsExtension args, Region region, int amount)
        {
            RegisterAction(new SetZ(new SetZArgs(args.Player, region, amount)), args.Player, region);
            return TShock.Regions.SetZ(region.Name, amount);
        }

        public bool Protect(CommandArgsExtension args, Region region, bool protect)
        {
            RegisterAction(new Protect(new ProtectArgs(args.Player, region, protect)), args.Player, region);
            return TShock.Regions.SetRegionState(region.Name, protect);
        }

        public bool Resize(CommandArgsExtension args, Region region, int amount, int direction)
        {
            RegisterAction(new Resize(new ResizeArgs(args.Player, region, amount, direction)), args.Player, region);
            return TShock.Regions.ResizeRegion(region.Name, amount, direction);
        }

        public bool ChangeOwner(CommandArgsExtension args, Region region, UserAccount account)
        {
            RegisterAction(new ChangeOwner(new ChangeOwnerArgs(args.Player, region, account)), args.Player, region);
            return TShock.Regions.ChangeOwner(region.Name, account.Name);
        }

        public bool DeleteRegion(TSPlayer user, Region region)
        {
            RegisterRegionDeletion(user, region);
            return TShock.Regions.DeleteRegion(region.Name);
        }

        public bool DefineRegion(TSPlayer user, Region region)
        {
            var result = TShock.Regions.AddRegion(
                region.Area.X,
                region.Area.Y,
                region.Area.Width,
                region.Area.Height,
                region.Name,
                region.Owner,
                region.WorldID,
                region.Z) &&
                TShock.Regions.SetRegionState(region.Name, region.DisableBuild);

            if (!result)
                return false;

            var definedRegion = TShock.Regions.GetRegionByName(region.Name);
            _infoManagerProvider()?.AddNewRegion(definedRegion.ID, user?.Account?.ID ?? 0);
            return true;
        }

        public List<string> GetRegionInfo(Region region) =>
            _infoManagerProvider()?.GetRegionInfo(region.ID);

        public List<string> GetRegionHistory(int count, Region region) =>
            _historyManagerProvider()?.GetActionsInfo(count, region.ID);

        public bool ClearAllowUsers(string regionName)
        {
            var region = TShock.Regions.GetRegionByName(regionName);
            if (region == null)
                return false;

            region.AllowedIDs.Clear();
            var ids = string.Join(",", region.AllowedIDs);
            return _tshockDatabase.Query(
                       "UPDATE Regions SET UserIds=@0 WHERE RegionName=@1 AND WorldID=@2",
                       ids,
                       regionName,
                       Main.worldID.ToString()) > 0;
        }

        public void RegisterCommand(TSPlayer executor, Region region)
        {
            var infoManager = _infoManagerProvider();
            infoManager?.UpdateLastUpdate(region.ID, DateTime.UtcNow);
            infoManager?.UpdateLastUser(region.ID, executor?.Account?.ID ?? 0);
        }

        private void RegisterRegionDeletion(TSPlayer user, Region region)
        {
            var infoManager = _infoManagerProvider();
            var info = infoManager != null && infoManager.TryGetRegionInfo(region.ID, out var loadedInfo)
                ? loadedInfo
                : new RegionExtensionInfo(region.ID, user?.Account?.ID ?? 0);

            _deletedRegionsProvider()?.RegisterDeletedRegion(region, user?.Account, info);
            infoManager?.RemoveRegion(region.ID);
        }

        private void RegisterAction(IAction action, TSPlayer executor, Region region)
        {
            RegisterCommand(executor, region);
            _historyManagerProvider()?.SaveAction(action, region, executor?.Account);
        }
    }
}
