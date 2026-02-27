using OTAPI;
using RegionExtension.Database;
using RegionExtension.Database.EventsArgs;
using RegionExtension.RegionTriggers.Actions;
using RegionExtension.RegionTriggers.Conditions;
using RegionExtension.RegionTriggers.RegionProperties;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
    
namespace RegionExtension.RegionTriggers
{
    public class TriggerManager
    {
        private readonly bool[] _triggerIgnores;
        private DateTime _lastUpdate = DateTime.UtcNow;
        private DatabaseTable<TriggerDBUnit> _database;
        private Region[] _lastRegions = new Region[TShock.Players.Length];
        private bool[] _lastHostile = new bool[TShock.Players.Length];
        Dictionary<int, List<Trigger>> _triggers = new Dictionary<int, List<Trigger>>();

        public static readonly RegionEvent[] Events = new RegionEvent[]
        {
            new RegionEvent(new[] {"onenter", "enter", "e" }, "OnEnterEventDesc", RegionEvents.OnEnter),
            new RegionEvent(new[] {"onleave", "leave", "l" }, "OnLeaveEventDesc", RegionEvents.OnLeave),
            new RegionEvent(new[] {"onin", "in", "i" }, "OnInEventDesc", RegionEvents.OnIn),
            new RegionEvent(new[] {"onpvpon", "pvpon"}, "OnPvpOnEventDesc", RegionEvents.OnPvpOn),
            new RegionEvent(new[] {"onpvpoff", "pvpoff"}, "OnPvpOffEventDesc", RegionEvents.OnPvpOff)
        };

        public TriggerManager(IDbConnection dbConnection, bool[] triggerIgnores)
        {
            _triggerIgnores = triggerIgnores;
            _database = new DatabaseTable<TriggerDBUnit>("RegionTrigger", dbConnection);
            Initialize();
        }

        public static event Action<TriggerActionArgs> OnEnter;
        public static event Action<TriggerActionArgs> OnLeave;
        public static event Action<TriggerActionArgs> OnIn;

        public static ActionFormer[] Formers { get => new ActionFormer[]
        {
            CommandAction.Former,
            PushAction.Former,
            SendPacketAction.Former,
            SendMessageAction.Former,
            SpawnNpc.Former,
            ProjectileSpawn.Former,
            GiveItem.Former,
            TeleportToPosition.Former,
            TeleportToWarp.Former,
            Kill.Former,
            BuffTrigger.Former,
            PvpMode.Former,
            ResetSection.Former
        };}

        public static ActionFormer GetFormer(string name)
        {
            var res = Formers.FirstOrDefault(f => f.Names.Contains(name.ToLower()));
            return res;
        }

        public void Initialize()
        {
            _database.InitializeTable();
            LoadTriggers();
        }

        private void LoadTriggers()
        {
            foreach (var region in TShock.Regions.Regions)
            {
                var list = _database.GetValues(TriggerDBUnit.Reader, new[] { (nameof(TriggerDBUnit.RegionId), (object)region.ID) }).Select(t => t.ParseToTrigger()).ToList();
                if (list.Count != 0)
                {
                    _triggers[region.ID] = list;
                    for (int i = 0; i < _triggers[region.ID].Count; i++)
                    {
                        if (_triggers[region.ID][i].LocalId != i)
                        {
                            _triggers[region.ID][i].LocalId = i;
                            _database.UpdateByColumn(nameof(TriggerDBUnit.LocalId), i, new[] { (nameof(TriggerDBUnit.Id), (object)_triggers[region.ID][i].Id) });
                        }
                    }
                }
            }
        }

        public void HandleRegionDeleted(Region region)
        {
            if (!_triggers.ContainsKey(region.ID))
                return;
            ClearTriggers(region);
        }

        public bool ClearTriggers(Region region)
        {
            _triggers.Remove(region.ID);
            return _database.RemoveByColumn(new[] { (nameof(TriggerDBUnit.RegionId), (object)region.ID) });
        }

        public void OnPlayerEnter(GreetPlayerEventArgs args)
        {
            _lastRegions[args.Who] = null;
        }

        public IEnumerable<Trigger> GetTriggers(Region region) =>
            _triggers.TryGetValue(region.ID, out var regionTriggers) ? regionTriggers : Enumerable.Empty<Trigger>();

        public bool CreateTrigger(Region region, RegionEvents regionEvent, ITriggerAction triggerAction)
        {
            if (!_triggers.ContainsKey(region.ID))
                _triggers[region.ID] = new List<Trigger>();
            if (!_database.SaveValue(new TriggerDBUnit(region.ID, _triggers[region.ID].Count, triggerAction.Name, regionEvent.ToString(), triggerAction.GetArgsString())))
                return false;
            var triggerUnit = _database.GetValues(TriggerDBUnit.Reader, new[] { (nameof(TriggerDBUnit.RegionId), (object)region.ID), (nameof(TriggerDBUnit.LocalId), _triggers[region.ID].Count) }).FirstOrDefault();
            if (triggerUnit == null)
                return false;
            var trigger = new Trigger(triggerUnit.Id, _triggers[region.ID].Count, region, regionEvent, triggerAction);
            _triggers[region.ID].Add(trigger);
            return true;
        }

        public bool RemoveTrigger(Region region, int id)
        {
            if (!_triggers.ContainsKey(region.ID) || _triggers[region.ID].Count <= id)
                return false;
            return RemoveTrigger(region, _triggers[region.ID][id]);
        }

        public bool RemoveTrigger(Region region, Trigger trigger)
        {
            if (!_database.RemoveByObject(new TriggerDBUnit(trigger)))
                return false;
            _triggers[region.ID].Remove(trigger);
            for(int i = 0; i < _triggers[region.ID].Count; i++)
            {
                _triggers[region.ID][i].LocalId = i;
                _database.UpdateByColumn(nameof(TriggerDBUnit.LocalId), i, new[] { (nameof(TriggerDBUnit.Id), (object)_triggers[region.ID][i].Id) });
            }
            return true;
        }

        public void OnUpdate()
        {
            if (DateTime.UtcNow < _lastUpdate.AddMilliseconds(500))
                return;
            for (int i = 0; i < TShock.Players.Length; i++)
            {
                var player = TShock.Players[i];
                if (player != null && player.Active && !_triggerIgnores[i])
                {
                    CheckRegionUpdate(player);
                    CheckPvpUpdate(player);
                }
            }
            _lastUpdate = DateTime.UtcNow;
        }

        private void CheckPvpUpdate(TSPlayer player)
        {
            if (player.TPlayer.hostile != _lastHostile[player.Index])
            {
                TriggerEvent(player.TPlayer.hostile ? RegionEvents.OnPvpOn : RegionEvents.OnPvpOff, player, player.CurrentRegion);
                _lastHostile[player.Index] = player.TPlayer.hostile;
            }
        }

        private void CheckRegionUpdate(TSPlayer player)
        {
            var currentRegion = player.CurrentRegion;
            var lastRegion = _lastRegions[player.Index];
            if (HasRegionChanged(player, currentRegion))
                ProcessRegionTransition(player, lastRegion, currentRegion);
            ProcessOnIn(player, currentRegion);
        }

        private bool HasRegionChanged(TSPlayer player, Region currentRegion) =>
            _lastRegions[player.Index] != currentRegion;

        private void ProcessRegionTransition(TSPlayer player, Region lastRegion, Region currentRegion)
        {
            ProcessOnEnter(player, currentRegion);
            ProcessOnLeave(player, lastRegion);
            _lastRegions[player.Index] = currentRegion;
        }

        private void ProcessOnEnter(TSPlayer player, Region region)
        {
            TriggerEvent(RegionEvents.OnEnter, player, region);
            OnEnter?.Invoke(new TriggerActionArgs(player, region));
        }

        private void ProcessOnLeave(TSPlayer player, Region region)
        {
            TriggerEvent(RegionEvents.OnLeave, player, region);
            OnLeave?.Invoke(new TriggerActionArgs(player, region));
        }

        private void ProcessOnIn(TSPlayer player, Region region)
        {
            TriggerEvent(RegionEvents.OnIn, player, region);
            OnIn?.Invoke(new TriggerActionArgs(player, region));
        }

        private void TriggerEvent(RegionEvents events, TSPlayer player, Region region)
        {
            if (region == null || !_triggers.TryGetValue(region.ID, out var regionTriggers))
                return;
            for (int i = 0; i < regionTriggers.Count; i++)
            {
                var trigger = regionTriggers[i];
                if (trigger.Event != events)
                    continue;
                if (!trigger.Conditions.CheckConditions(player, region, trigger))
                    continue;
                trigger.Action.Execute(new TriggerActionArgs(player, region));
            }
        }

        public bool AddCondition(Region region, IRegionCondition condition, IEnumerable<int> localIds)
        {
            var triggers = _triggers[region.ID];
            localIds ??= triggers.Select(t => t.LocalId);
            var res = true;
            foreach (var i in localIds.Intersect(triggers.Select(t => t.LocalId)))
            {
                triggers[i].Conditions = triggers[i].Conditions.Where(c => !c.GetNames()[0].Equals(condition.GetNames()[0]))
                                                               .Append(condition)
                                                               .ToList();
                res = true && _database.UpdateByColumn(nameof(TriggerDBUnit.Conditions), ConditionManager.GenerateConditionsString(triggers[i].Conditions), new[] { (nameof(TriggerDBUnit.Id), (object)triggers[i].Id) });
            }
            return res;
        }

        public bool RemoveCondition(Region region, IRegionCondition condition, IEnumerable<int> localIds)
        {
            var triggers = _triggers[region.ID];
            localIds ??= triggers.Select(t => t.LocalId);
            var res = true;
            foreach (var i in localIds.Intersect(triggers.Select(t => t.LocalId)))
            {
                triggers[i].Conditions = triggers[i].Conditions.Where(c => !c.GetNames()[0].Equals(condition.GetNames()[0])).ToList();
                res = _database.UpdateByColumn(nameof(TriggerDBUnit.Conditions), ConditionManager.GenerateConditionsString(triggers[i].Conditions), new[] { (nameof(TriggerDBUnit.Id), (object)triggers[i].Id) });
            }
            return res;
        }

        public void Reload(ReloadEventArgs e)
        {
            _triggers = new Dictionary<int, List<Trigger>>();
            LoadTriggers();
        }
    }


    public enum RegionEvents
    {
        None,
        OnEnter,
        OnLeave,
        OnIn,
        OnPvpOn,
        OnPvpOff
    }

    public class RegionEvent
    {
        public string[] Names { get; }
        public string Description { get; }
        public RegionEvents Event { get; }
        public RegionEvent(string[] names, string desc, RegionEvents regionEvent)
        {
            Names = names;
            Description = desc;
            Event = regionEvent;
        }
    }
}

