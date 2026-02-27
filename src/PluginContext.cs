using System.Collections.Generic;
using RegionExtension.Database;
using Terraria;

namespace RegionExtension
{
    public sealed class PluginContext
    {
        public ContextManager Contexts { get; set; }
        public List<FastRegion> FastRegions { get; set; } = new List<FastRegion>();
        public ConfigFile Config { get; set; } = new ConfigFile();
        public RegionExtManager RegionManager { get; set; }
        public ItemRewrite[] ItemRewrites { get; } = new ItemRewrite[Main.maxItems];
    }
}
