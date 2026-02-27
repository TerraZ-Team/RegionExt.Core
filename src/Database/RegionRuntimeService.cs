using Microsoft.Xna.Framework;
using RegionExtension.Database.Modules;
using System;
using System.Collections.Generic;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace RegionExtension.Database
{
    internal sealed class RegionRuntimeService
    {
        public RegionRuntimeService(
            PluginContext context,
            Func<IRegionRequestManager> requestManagerProvider,
            Func<IRegionTriggerManager> triggerManagerProvider,
            Func<IRegionPropertyManager> propertyManagerProvider,
            Func<Region, UserAccount, bool, bool> removeRequest)
        {
        }

        public void Update()
        {
        }

        public void Reload(ReloadEventArgs e)
        {
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
