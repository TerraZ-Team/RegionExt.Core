using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using TShockAPI;
using TShockAPI.Hooks;

namespace RegionExtension.Database
{
    internal sealed class RegionRuntimeService
    {
        public RegionRuntimeService(PluginContext context)
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
