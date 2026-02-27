using Google.Protobuf.WellKnownTypes;
using NuGet.Protocol.Plugins;
using RegionExtension.RegionTriggers.Actions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerrariaApi.Server;
using TShockAPI;

namespace RegionExtension.RegionTriggers.Conditions
{
    public static class DelayManager
    {
        private static readonly object _lock = new object();
        private static SortedList<DateTime, DelayInfo> ForcedDelayedTriggers { get; set; } = new SortedList<DateTime, DelayInfo>();
        private static SortedList<DateTime, DelayInfo> InRegionDelayedTriggers { get; set; } = new SortedList<DateTime, DelayInfo>();
        private static SortedList<DateTime, DelayInfo> AlwaysInRegionDelayedTriggers { get; set; } = new SortedList<DateTime, DelayInfo>();

        private static DateTime _lastUpdate;

        public static Dictionary<string, DelayType> DelayTypes { get; private set; } = new Dictionary<string, DelayType>
        {
            {"-f", DelayType.Forced },
            {"-i", DelayType.InRegionOnActive },
            {"-a", DelayType.InRegion }
        };

        public static void Initialize(TerrariaPlugin plugin)
        {
            ServerApi.Hooks.GameUpdate.Register(plugin, OnUpdate);
            TriggerManager.OnLeave += OnLeave;
        }

        private static void OnLeave(TriggerActionArgs args)
        {
            var hasPlayersInRegion = TShock.Players.Any(p => p != null && p.Active && p.CurrentRegion == args.Region);
            lock (_lock)
            {
                foreach (var item in AlwaysInRegionDelayedTriggers.Where(i => i.Value.Region == args.Region).ToArray())
                    if (item.Value.RecheckPlayer)
                        AlwaysInRegionDelayedTriggers.Remove(item.Key);
                    else if (!hasPlayersInRegion)
                        AlwaysInRegionDelayedTriggers.Remove(item.Key);
            }

        }

        private static void OnUpdate(EventArgs args)
        {
            var now = DateTime.Now;
            if (now < _lastUpdate.AddMilliseconds(1000))
                return;

            var forced = PopReadyTriggers(ForcedDelayedTriggers, now);
            var inRegion = PopReadyTriggers(InRegionDelayedTriggers, now);
            var alwaysInRegion = PopReadyTriggers(AlwaysInRegionDelayedTriggers, now);

            foreach (var value in forced)
                value.Trigger.Action.Execute(new TriggerActionArgs(value.Player, value.Region));

            foreach (var value in inRegion)
            {
                if ((value.RecheckPlayer && value.Player.CurrentRegion == value.Region) ||
                    (!value.RecheckPlayer && TShock.Players.Any(p => p != null && p.Active && p.CurrentRegion == value.Region)))
                    value.Trigger.Action.Execute(new TriggerActionArgs(value.Player, value.Region));
            }

            foreach (var value in alwaysInRegion)
                value.Trigger.Action.Execute(new TriggerActionArgs(value.Player, value.Region));

            _lastUpdate = now;
        }

        public static void Reload(TerrariaPlugin plugin)
        {
            lock (_lock)
            {
                ForcedDelayedTriggers = new SortedList<DateTime, DelayInfo>();
                InRegionDelayedTriggers = new SortedList<DateTime, DelayInfo>();
                AlwaysInRegionDelayedTriggers = new SortedList<DateTime, DelayInfo>();
            }
        }

        public static void RegisterDelay(DelayInfo delay, string delayFlag, DateTime activation)
        {
            RegisterDelay(delay, DelayTypes[delayFlag], activation);
        }

        public static DelayType GetType(string delayFlag)
        {
            if (!DelayTypes.ContainsKey(delayFlag.ToLower()))
                return DelayType.None;
            return DelayTypes[delayFlag];
        }

        public static void RegisterDelay(DelayInfo delay, DelayType delayType, DateTime activation)
        {
            if (delay.Trigger == null || !delay.Trigger.Conditions.Where(c => c.GetNames()[0] != Delay.Names[0] && c.GetNames()[0] != PlayerDelay.Names[0]).CheckConditions(delay.Player, delay.Region))
                return;
            lock (_lock)
            {
                var targetList = ForcedDelayedTriggers;
                switch (delayType)
                {
                    case DelayType.None:
                        break;
                    case DelayType.Forced:
                        targetList = ForcedDelayedTriggers;
                        break;
                    case DelayType.InRegionOnActive:
                        targetList = InRegionDelayedTriggers;
                        break;
                    case DelayType.InRegion:
                        targetList = AlwaysInRegionDelayedTriggers;
                        break;
                }

                if ((delay.RecheckPlayer && targetList.Any(p => p.Value.Player == delay.Player && p.Value.Trigger == delay.Trigger)) ||
                   (!delay.RecheckPlayer && targetList.Any(p => p.Value.Region == delay.Region && p.Value.Trigger == delay.Trigger)))
                    return;

                while (targetList.ContainsKey(activation))
                    activation = activation.AddTicks(1);
                targetList.Add(activation, delay);
            }
        }

        public static void Dispose(TerrariaPlugin plugin)
        {
            ServerApi.Hooks.GameUpdate.Deregister(plugin, OnUpdate);
            TriggerManager.OnLeave -= OnLeave;
        }

        private static List<DelayInfo> PopReadyTriggers(SortedList<DateTime, DelayInfo> list, DateTime now)
        {
            var res = new List<DelayInfo>();
            lock (_lock)
            {
                while (list.Count > 0 && list.Keys[0] < now)
                {
                    res.Add(list.Values[0]);
                    list.RemoveAt(0);
                }
            }
            return res;
        }
    }

    public enum DelayType
    {
        None,
        Forced,
        InRegionOnActive,
        InRegion
    }
}

