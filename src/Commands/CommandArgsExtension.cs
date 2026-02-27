using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace RegionExtension.Commands
{
    public class CommandArgsExtension : CommandArgs
    {
        public Plugin Plugin { get; set; }
        public PluginContext Context { get; }

        public CommandArgsExtension(CommandArgs args, Plugin plugin, PluginContext context) :
            base(args.Message, args.Silent, args.Player, args.Parameters)
        {
            Plugin = plugin;
            Context = context;
        }
    }
}

