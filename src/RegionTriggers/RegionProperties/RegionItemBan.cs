using Terraria.ID;
using RegionExtension.Commands.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using TShockAPI.Localization;
using RegionExtension.RegionTriggers.Conditions;

namespace RegionExtension.RegionTriggers.RegionProperties
{
    public class RegionItemBan : IRegionProperty
    {
        public string[] Names => new[] { "itemban", "ib"};
        public string Description => "ItemBanPropDesc";
        public string Permission => Permissions.PropertyItem;
        public ICommandParam[] CommandParams => new[] { new ArrayParam<Item>("items...", "Items which will be banned in region.")};
        public Region[] DefinedRegions =>_itemsBan.Keys.ToArray();

        private Dictionary<Region, ConditionDataPair<int>> _itemsBan = new Dictionary<Region, ConditionDataPair<int>>(RegionIdComparer.Instance);
        private Dictionary<Region, HashSet<int>> _itemsBanHash = new Dictionary<Region, HashSet<int>>(RegionIdComparer.Instance);
        private DateTime _lastUpdate = DateTime.Now;
        private readonly bool[] _triggerIgnores;

        public RegionItemBan(bool[] triggerIgnores = null)
        {
            _triggerIgnores = triggerIgnores ?? new bool[Main.maxPlayers];
        }

        public void InitializeEventHandler(TerrariaPlugin plugin)
        {
            ServerApi.Hooks.GamePostUpdate.Register(plugin, OnPostUpdate);
        }

        private void OnPostUpdate(EventArgs args)
        {
            if (DateTime.Now.AddSeconds(-2) < _lastUpdate)
                return;
            foreach(var plr in TShock.Players.Where(p => p != null && p.Active && !_triggerIgnores[p.Index]))
                CheckItemBan(plr);
            _lastUpdate = DateTime.Now;
        }

        public void CheckItemBan(TSPlayer player)
        {
            var reg = player.CurrentRegion;
            if (reg == null || !_itemsBan.ContainsKey(reg))
                return;
            var items = _itemsBan[reg];
            var bannedSet = _itemsBanHash[reg];
            if(!items.Conditions.CheckConditions(player, reg))
                return;
            if (bannedSet.Contains(player.TPlayer.inventory[player.TPlayer.selectedItem].type))
            {
                string itemName = player.TPlayer.inventory[player.TPlayer.selectedItem].Name;
                Taint(player);
                SendCorrectiveMessage(player, itemName);
            }
            if (!Main.ServerSideCharacter || (Main.ServerSideCharacter && player.IsLoggedIn))
            {
                CheckItemInventoryBan(player, player.TPlayer.armor, bannedSet);
                CheckItemInventoryBan(player, player.TPlayer.dye, bannedSet);
                CheckItemInventoryBan(player, player.TPlayer.miscEquips, bannedSet);
                CheckItemInventoryBan(player, player.TPlayer.miscDyes, bannedSet);
            }
        }

        private void Taint(TSPlayer player)
        {
            player.SetBuff(BuffID.Frozen, 120, true);
            player.SetBuff(BuffID.Stoned, 330, true);
            player.SetBuff(BuffID.Webbed, 330, true);
        }

        private void CheckItemInventoryBan(TSPlayer player, IEnumerable<Item> playerItems, HashSet<int> bannedSet)
        {
            foreach(var item in playerItems)
            {
                if (bannedSet.Contains(item.type))
                {
                    Taint(player);
                    SendCorrectiveMessage(player, item.Name);
                }
            }
        }

        private void SendCorrectiveMessage(TSPlayer player, string itemName)
        {
            player.SendErrorMessage("{0} is banned in this region! Remove it!".SFormat(itemName));
        }

        public void AddRegionProperties(Region region, ICommandParam[] commandParams)
        {
            var itemsToBan = ((Item[])commandParams[0].Value).Select(i => i.type);
            if(!_itemsBan.ContainsKey(region))
                _itemsBan.Add(region, new(new List<IRegionCondition>(), new List<int>()));
            _itemsBan[region].Data.AddRange(itemsToBan);
            _itemsBan[region].Data = _itemsBan[region].Data.GroupBy(x => x).Select(x => x.First()).ToList();
            _itemsBan[region].Data.Sort();
            _itemsBanHash[region] = new HashSet<int>(_itemsBan[region].Data);
        }

        public void RemoveRegionProperties(Region region, ICommandParam[] commandParams)
        {
            var itemsToBan = (Item[])commandParams[0].Value;
            if (!_itemsBan.ContainsKey(region))
                return;
            _itemsBan[region].Data.RemoveAll(i => itemsToBan.Select(i => i.type).Contains(i));
            if (_itemsBan[region].Data.Count < 1)
            {
                _itemsBan.Remove(region);
                _itemsBanHash.Remove(region);
            }
            else
                _itemsBanHash[region] = new HashSet<int>(_itemsBan[region].Data);
        }

        public void SetFromString(Region region, ConditionStringPair args)
        {
           if (!_itemsBan.ContainsKey(region))
                _itemsBan.Add(region, ConditionDataPair<int>.GetFromString(args));
           _itemsBanHash[region] = new HashSet<int>(_itemsBan[region].Data);
        }

        public ConditionStringPair GetStringArgs(Region region) =>
            _itemsBan[region]?.ConvertToString();

        public void ClearProperties(Region region)
        {
            _itemsBan.Remove(region);
            _itemsBanHash.Remove(region);
        }

        public void AddCondition(Region region, ICommandParam[] commandParams, IRegionCondition condition)
        {
            var itemsToBan = ((Item[])commandParams[0].Value).Select(i => i.type);
            if (!_itemsBan.ContainsKey(region))
                return;
            _itemsBan[region].Conditions = _itemsBan[region].Conditions.Where(p => !p.GetNames()[0].Equals(condition.GetNames()[0])).Append(condition).ToList();
        }

        public void RemoveCondition(Region region, ICommandParam[] commandParams, IRegionCondition condition)
        {
            var itemsToBan = ((Item[])commandParams[0].Value).Select(i => i.type);
            if (!_itemsBan.ContainsKey(region))
                return;
            _itemsBan[region].Conditions = _itemsBan[region].Conditions.Where(p => !p.GetNames()[0].Equals(condition.GetNames()[0])).ToList();
        }

        public void Dispose(TerrariaPlugin plugin)
        {
            ServerApi.Hooks.GamePostUpdate.Deregister(plugin, OnPostUpdate);
        }
    }
}

