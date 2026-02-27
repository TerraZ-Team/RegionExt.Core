using Terraria;

namespace RegionExtension.Infrastructure
{
    public static class ItemRewriteRegistry
    {
        public static ItemRewrite[] Rewrites { get; } = new ItemRewrite[Main.maxItems];
    }
}
