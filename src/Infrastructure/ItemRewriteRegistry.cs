using Terraria;

namespace RegionExtension.Infrastructure
{
    internal static class ItemRewriteRegistry
    {
        public static ItemRewrite[] Rewrites { get; } = new ItemRewrite[Main.maxItems];
    }
}
