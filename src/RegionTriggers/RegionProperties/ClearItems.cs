using RegionExtension.Commands.Parameters;
using RegionExtension.RegionTriggers.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace RegionExtension.RegionTriggers.RegionProperties
{
    internal class ClearItems : IRegionProperty
    {
        public string[] Names => new[] { "clearitems", "ci" };
        public string Description => "ClearItemsPropDesc";
        public string Permission => Permissions.PropertyBlockTileFrame;
        public ICommandParam[] CommandParams => new ICommandParam[0];
        public Region[] DefinedRegions => _regions.Keys.ToArray();

        private Dictionary<Region, List<IRegionCondition>> _regions = new Dictionary<Region, List<IRegionCondition>>(RegionIdComparer.Instance);
        private DateTime _lastUpdate;

        public void InitializeEventHandler(TerrariaPlugin plugin)
        {
            ServerApi.Hooks.GameUpdate.Register(plugin, OnUpdate);
        }

        private void OnUpdate(EventArgs args)
        {
            if (DateTime.UtcNow.AddSeconds(-2) < _lastUpdate)
                return;
            var regions = _regions.Keys.ToArray();
            if (regions.Length == 0)
                return;
            for (int i = 0; i < Terraria.Main.item.Length; i++)
            {
                var item = Terraria.Main.item[i];
                if (item == null || !item.active)
                    continue;
                int tileX = (int)Math.Floor(item.position.X / 16);
                int tileY = (int)Math.Floor(item.position.Y / 16);
                if (!IsInAnyRegion(regions, tileX, tileY))
                    continue;
                item.TurnToAir();
                NetMessage.SendData((int)PacketTypes.UpdateItemDrop, number:item.whoAmI);
            }
            _lastUpdate = DateTime.UtcNow;
        }

        private static bool IsInAnyRegion(Region[] regions, int tileX, int tileY)
        {
            for (int i = 0; i < regions.Length; i++)
                if (regions[i].InArea(tileX, tileY))
                    return true;
            return false;
        }

        public void AddRegionProperties(Region region, ICommandParam[] commandParams)
        {
            if (!_regions.ContainsKey(region))
                _regions.Add(region, new List<IRegionCondition>());
        }

        public void RemoveRegionProperties(Region region, ICommandParam[] commandParams)
        {
            if (_regions.ContainsKey(region))
                _regions.Remove(region);
        }

        public void SetFromString(Region region, ConditionStringPair args)
        {
            if (!_regions.ContainsKey(region))
                _regions.Add(region, ConditionDataPair<int>.GetFromString(args).Conditions);
        }

        public ConditionStringPair GetStringArgs(Region region) =>
            new ConditionDataPair<int>(_regions[region], new List<int> { 1 }).ConvertToString();

        public void ClearProperties(Region region) =>
            _regions.Remove(region);

        public void AddCondition(Region region, ICommandParam[] commandParams, IRegionCondition condition)
        {
            if (!_regions.ContainsKey(region))
                return;
            _regions[region] = _regions[region].Where(p => !p.GetNames()[0].Equals(condition.GetNames()[0])).Append(condition).ToList();
        }

        public void RemoveCondition(Region region, ICommandParam[] commandParams, IRegionCondition condition)
        {
            if (!_regions.ContainsKey(region))
                return;
            _regions[region] = _regions[region].Where(p => !p.GetNames()[0].Equals(condition.GetNames()[0])).ToList();
        }

        public void Dispose(TerrariaPlugin plugin)
        {
            ServerApi.Hooks.GameUpdate.Deregister(plugin, OnUpdate);
        }
    }
}

