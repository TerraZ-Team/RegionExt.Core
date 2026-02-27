using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace RegionExtension.Database
{
    internal sealed class RegionRuntimeService
    {
        private readonly PluginContext _context;
        private readonly Func<RegionRequestManager> _requestManagerProvider;
        private readonly Func<RegionTriggers.TriggerManager> _triggerManagerProvider;
        private readonly Func<RegionTriggers.PropertyManager> _propertyManagerProvider;
        private readonly Func<Region, UserAccount, bool, bool> _removeRequest;

        private DateTime _lastUpdate = DateTime.UtcNow;
        private DateTime _lastNotify = DateTime.UtcNow;

        public RegionRuntimeService(
            PluginContext context,
            Func<RegionRequestManager> requestManagerProvider,
            Func<RegionTriggers.TriggerManager> triggerManagerProvider,
            Func<RegionTriggers.PropertyManager> propertyManagerProvider,
            Func<Region, UserAccount, bool, bool> removeRequest)
        {
            _context = context;
            _requestManagerProvider = requestManagerProvider;
            _triggerManagerProvider = triggerManagerProvider;
            _propertyManagerProvider = propertyManagerProvider;
            _removeRequest = removeRequest;
        }

        public void Update()
        {
            var requestManager = _requestManagerProvider();
            var triggerManager = _triggerManagerProvider();

            triggerManager?.OnUpdate();

            if (DateTime.UtcNow < _lastUpdate.AddSeconds(10))
                return;

            if (requestManager != null)
            {
                var requestsToRemove = requestManager.Requests.Where(r =>
                {
                    var time = StringTime.FromString(Utils.GetSettingsByUserAccount(_context.Config, r.User).RequestTime);
                    if (time.IsZero())
                        return false;
                    return r.DateCreation + time < DateTime.UtcNow;
                }).ToArray();

                foreach (var request in requestsToRemove)
                {
                    _removeRequest(request.Region, null, Utils.GetSettingsByUserAccount(_context.Config, request.User).AutoApproveRequest);
                }

                var timePeriod = StringTime.FromString(_context.Config.NotificationPeriod);
                if (!timePeriod.IsZero() && _lastNotify + timePeriod < DateTime.UtcNow)
                {
                    var players = TShock.Players.Where(p => p != null && p.Account != null && p.HasPermission(Permissions.RegionExtCmd));
                    foreach (var player in players)
                        SendRequestNotify(player, requestManager.GetSortedRegionRequestsNames(_context.Config));
                    _lastNotify = DateTime.UtcNow;
                }
            }

            _lastUpdate = DateTime.UtcNow;
        }

        public void Reload(ReloadEventArgs e)
        {
            var triggerManager = _triggerManagerProvider();
            var propertyManager = _propertyManagerProvider();
            triggerManager?.Reload(e);
            propertyManager?.Reload(e);
        }

        public void SendRequestNotify(TSPlayer player, IEnumerable<string> strings)
        {
            PaginationTools.SendPage(player, 0, PaginationTools.BuildLinesFromTerms(strings, null, ", ", 240), new PaginationTools.Settings()
            {
                HeaderFormat = "Active region requests:",
                IncludeFooter = false,
                LineTextColor = Color.White
            });
        }
    }
}
