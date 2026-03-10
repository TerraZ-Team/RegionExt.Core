using System;
using System.Collections.Generic;
using System.Linq;
using Multiplicity.Packets;
using RegionExtension.Database;
using RegionExtension.Commands.Parameters;
using Multiplicity.Packets.Views;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using MultiplicityPacketTypes = Multiplicity.Packets.PacketTypes;

namespace RegionExtension.Infrastructure
{
    internal sealed class PluginEventDispatcher
    {
        private readonly Plugin _plugin;
        private readonly PluginContext _context;
        private readonly object _lastActiveLock = new object();
        private bool _checkingHasBuild;
        private bool _handlingItemDrop;
        private List<Point16> _lastActive = new List<Point16>();
        private DateTime _lastActiveCheck = DateTime.UtcNow;

        public PluginEventDispatcher(Plugin plugin, PluginContext context)
        {
            _plugin = plugin;
            _context = context;
        }

        public void Register()
        {
            ServerApi.Hooks.GameInitialize.Register(_plugin, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(_plugin, OnGetData);
            ServerApi.Hooks.GamePostInitialize.Register(_plugin, OnPostInitialize, int.MinValue);
            ServerApi.Hooks.GamePostUpdate.Register(_plugin, OnPostUpdate);
            ServerApi.Hooks.NetSendData.Register(_plugin, OnSendData);
            GeneralHooks.ReloadEvent += OnReload;
            PlayerHooks.PlayerLogout += OnPlayerLogout;
            PlayerHooks.PlayerCommand += OnPlayerCommand;
            PlayerHooks.PlayerHasBuildPermission += OnHasPlayerPermission;
            _context.RegionManager = new RegionExtManager(TShock.DB, context: _context);
        }

        public void Deregister()
        {
            ServerApi.Hooks.GameInitialize.Deregister(_plugin, OnInitialize);
            ServerApi.Hooks.NetGetData.Deregister(_plugin, OnGetData);
            ServerApi.Hooks.GamePostInitialize.Deregister(_plugin, OnPostInitialize);
            ServerApi.Hooks.GamePostUpdate.Deregister(_plugin, OnPostUpdate);
            ServerApi.Hooks.NetSendData.Deregister(_plugin, OnSendData);
            GeneralHooks.ReloadEvent -= OnReload;
            PlayerHooks.PlayerLogout -= OnPlayerLogout;
            PlayerHooks.PlayerCommand -= OnPlayerCommand;
            PlayerHooks.PlayerHasBuildPermission -= OnHasPlayerPermission;
        }

        private void OnReload(ReloadEventArgs e)
        {
            try
            {
                _context.Config = ConfigFile.Read();
                Localization.DefaultLocalization = _context.Config.DefaultLocalization;
                _context.RegionManager?.Reload(e);
                e.Player?.SendInfoMessage("[RegionExt] Config reloaded.");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[RegionExt] Reload failed: {ex}");
                e.Player?.SendErrorMessage("[RegionExt] Reload failed. Check server logs.");
            }
        }

        private void OnSendItemDrop(SendDataEventArgs args)
        {
            var id = args.number;
            var rewrites = ItemRewriteRegistry.Rewrites;
            if (id >= Main.maxItems || rewrites[id] == null || !rewrites[id].Active)
                return;

            args.Handled = true;
            var bits = new BitsByte(b2: rewrites[id].Damage != -1, b5: rewrites[id].UseTime != -1, b6: rewrites[id].Projectile != -1, b7: rewrites[id].Projectile != -1, b8: rewrites[id].Projectile != -1);
            var bits2 = new BitsByte(b5: true);
            if (rewrites[id].Damage != -1)
                Main.item[id].inner.damage = rewrites[id].Damage;
            if (rewrites[id].UseTime != -1)
                Main.item[id].inner.useTime = rewrites[id].UseTime;
            if (rewrites[id].Projectile != -1)
            {
                Main.item[id].inner.shoot = rewrites[id].Projectile;
                Main.item[id].inner.useAmmo = AmmoID.None;
                if (Main.item[id].inner.shootSpeed == 0)
                    Main.item[id].inner.shootSpeed = 10;
            }
            NetMessage.SendData((int)args.MsgId, args.remoteClient, args.ignoreClient, args.text, args.number, args.number2, args.number3, args.number4, args.number5, args.number6, args.number7);
            NetMessage.SendData((int)PacketTypes.TweakItem, -1, -1, null, id, bits.value, bits2.value);
        }

        private void OnSendData(SendDataEventArgs args)
        {
            switch ((int)args.MsgId)
            {
                case (int)PacketTypes.ItemDrop:
                case (int)PacketTypes.UpdateItemDrop:
                case (int)PacketTypes.SyncItemsWithShimmer:
                case (int)PacketTypes.SyncItemCannotBeTakenByEnemies:
                    if (_handlingItemDrop)
                        return;
                    _handlingItemDrop = true;
                    try
                    {
                        OnSendItemDrop(args);
                    }
                    finally
                    {
                        _handlingItemDrop = false;
                    }
                    break;
            }
        }

        private void OnPostUpdate(EventArgs args)
        {
            _context.RegionManager?.Update();
            UpdateLastActive();
        }

        private void OnPostInitialize(EventArgs args)
        {
            InitializePlugin();
        }

        private void InitializePlugin()
        {
            _context.RegionManager.PostInitialize(_plugin);
            TShock.Log.ConsoleInfo("Region extension loaded!");
        }

        private void OnHasPlayerPermission(PlayerHasBuildPermissionEventArgs e)
        {
            if (_checkingHasBuild)
            {
                e.Result = PermissionHookResult.Unhandled;
                return;
            }
            _checkingHasBuild = true;
            if (e.Player.HasBuildPermission(e.X, e.Y, true) && TShock.Regions.InArea(e.X, e.Y))
            {
                lock (_lastActiveLock)
                    _lastActive.Add(new Point16(e.X, e.Y));
            }
            _checkingHasBuild = false;
        }

        private void UpdateLastActive()
        {
            if (DateTime.UtcNow < _lastActiveCheck.AddSeconds(90))
                return;
            List<Point16> points;
            lock (_lastActiveLock)
            {
                points = _lastActive;
                _lastActive = new List<Point16>();
            }
            var regionsToUpdate = new HashSet<int>();
            foreach (var point in points)
                foreach (var id in TShock.Regions.InAreaRegionID(point.X, point.Y))
                    regionsToUpdate.Add(id);
            foreach (var id in regionsToUpdate)
                _context.RegionManager.InfoManager.UpdateLastActivity(id, DateTime.UtcNow);
            points.Clear();
            _lastActiveCheck = DateTime.UtcNow;
        }

        private void OnInitialize(EventArgs args)
        {
            _context.Config = ConfigFile.Read();
            Localization.DefaultLocalization = _context.Config.DefaultLocalization;
            PluginCommands.Initialize(_plugin, _context);
            _context.Contexts = new ContextManager(_context);
            _context.Contexts.Initialize();
            _context.FastRegions = new List<FastRegion>();
        }

        private void OnPlayerLogout(PlayerLogoutEventArgs e)
        {
            int id = FastRegionLookup.FindByUser(e.Player.Account, _context.FastRegions);
            if (id != -1)
                _context.FastRegions.RemoveAt(id);
        }

        private void OnGetData(GetDataEventArgs args)
        {
            switch (args.MsgID)
            {
                case PacketTypes.MassWireOperation:
                    HandleMassWireOperation(args);
                    break;
                case PacketTypes.Tile:
                    HandleTileOperation(args);
                    break;
                case PacketTypes.ItemDrop:
                case PacketTypes.UpdateItemDrop:
                    HandleItemDropOperation(args);
                    break;
            }
        }

        private void HandleMassWireOperation(GetDataEventArgs args)
        {
            if (!TryGetFastRegionIndex(args.Msg.whoAmI, out var id))
                return;

            if (!TryParseExactPayload(MultiplicityPacketTypes.MassWireOperation, args, out var packetView))
                return;

            var view = packetView.AsMassWireOperationView();
            int startX = view.StartX;
            int startY = view.StartY;
            int endX = view.EndX;
            int endY = view.EndY;
            if (!IsInWorldBounds(startX, startY) || !IsInWorldBounds(endX, endY))
                return;
            if (_context.FastRegions[id].SetPoints(startX, startY, endX, endY))
                _context.FastRegions.RemoveAt(id);

            args.Handled = true;
        }

        private void HandleTileOperation(GetDataEventArgs args)
        {
            if (!TryGetFastRegionIndex(args.Msg.whoAmI, out var id))
                return;

            if (!TryParseExactPayload(MultiplicityPacketTypes.Tile, args, out var packetView))
                return;

            var view = packetView.AsTileView();
            int x = view.TileX;
            int y = view.TileY;
            if (!IsInWorldBounds(x, y))
                return;
            if (_context.FastRegions[id].SetPoint(x, y))
                _context.FastRegions.RemoveAt(id);

            args.Handled = true;
        }

        private void HandleItemDropOperation(GetDataEventArgs args)
        {
            var packetType = (MultiplicityPacketTypes)(byte)args.MsgID;
            if (!TryParseEventPayload(packetType, args, out var packetView))
                return;

            var view = packetView.AsWorldItemSyncView();
            int id = view.ItemIndex;
            var rewrites = ItemRewriteRegistry.Rewrites;
            if (id >= Main.maxItems || rewrites[id] == null || !rewrites[id].Active)
                return;
            if (view.Stack == 0)
                rewrites[id].Active = false;
        }

        private static bool IsInWorldBounds(int x, int y) =>
            x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY;

        private static bool TryParseExactPayload(MultiplicityPacketTypes packetType, GetDataEventArgs args, out PacketView packetView)
        {
            return PacketViewParser.TryParsePayload(packetType, args.Msg.readBuffer, args.Index, args.Length, out packetView);
        }

        private static bool TryParseEventPayload(MultiplicityPacketTypes packetType, GetDataEventArgs args, out PacketView packetView)
        {
            if (!PacketViewParser.TryParsePayload(packetType, args.Msg.readBuffer, args.Index, out packetView, out int consumed))
            {
                packetView = default;
                return false;
            }

            if (consumed > args.Length)
            {
                packetView = default;
                return false;
            }

            return true;
        }

        private bool TryGetFastRegionIndex(int whoAmI, out int id)
        {
            id = FastRegionLookup.FindByUser(TShock.Players[whoAmI]?.Account, _context.FastRegions);
            return id != -1;
        }

        private void OnPlayerCommand(PlayerCommandEventArgs args)
        {
            if (!args.Player.HasPermission(Permissions.RegionExtCmd) && !args.Player.HasPermission(Permissions.RegionOwnCmd))
                return;
            switch (args.CommandName)
            {
                case "re":
                case "regionext":
                case "ro":
                    case "regionown":
                case "region":
                    for (int i = 1; i < args.Parameters.Count; i++)
                        if (args.Parameters[i].StartsWith(_context.Config.ContextSpecifier))
                            _context.Contexts.InitializeContext(i, args);
                    if (_context.Config.AutoCompleteSameName && args.Parameters.Count > 1 && "define" == args.Parameters[0])
                        args.Parameters[1] = Utils.AutoCompleteSameName(args.Parameters[1], _context.Config.AutoCompleteSameNameFormat);
                    break;
            }
        }
    }
}



