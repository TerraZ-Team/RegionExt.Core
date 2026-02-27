using RegionExtension.Commands;
using RegionExtension.Commands.SubCommands;
using TerrariaApi.Server;

namespace RegionExtension
{
    public static class RegionExtCoreApi
    {
        private const string RequestsModuleId = "RegionExt.Requests";
        private const string TriggersModuleId = "RegionExt.Triggers";

        public static bool IsReady => PluginCommands.Context?.RegionManager != null;

        public static bool EnableRequestsModule()
        {
            var manager = PluginCommands.Context?.RegionManager;
            if (manager == null || !manager.EnableRequests())
                return false;

            PluginCommands.RegisterModuleRegionSubCommands(
                RequestsModuleId,
                static () => new RequestListSubCommand(),
                static () => new RequestInfoSubCommand(),
                static () => new RequestAcceptSubCommand(),
                static () => new RequestDenySubCommand());

            PluginCommands.RegisterModuleRegionOwnSubCommands(
                RequestsModuleId,
                static () => new DefineRequestSubCommand(),
                static () => new FastRegionRequestSubCommand());

            PluginCommands.Rebuild();
            return true;
        }

        public static void DisableRequestsModule()
        {
            PluginCommands.Context?.RegionManager?.DisableRequests();
            PluginCommands.UnregisterModule(RequestsModuleId);
            PluginCommands.Rebuild();
        }

        public static bool EnableTriggersModule(TerrariaPlugin plugin)
        {
            var manager = PluginCommands.Context?.RegionManager;
            if (manager == null || !manager.EnableTriggers(plugin))
                return false;

            PluginCommands.RegisterModuleRootCommands(
                TriggersModuleId,
                static () => new RegionTriggerCommand(),
                static () => new RegionProperty());

            PluginCommands.Rebuild();
            return true;
        }

        public static void DisableTriggersModule(TerrariaPlugin plugin)
        {
            PluginCommands.Context?.RegionManager?.DisableTriggers(plugin);
            PluginCommands.UnregisterModule(TriggersModuleId);
            PluginCommands.Rebuild();
        }
    }
}
