using RegionExtension.Commands;
using RegionExtension.Commands.SubCommands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RegionExtension
{
    public static class PluginCommands
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Func<CommandExtension>[]> ModuleRootCommands = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Func<ISubCommand>[]> ModuleRegionSubCommands = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Func<ISubCommand>[]> ModuleRegionOwnSubCommands = new(StringComparer.OrdinalIgnoreCase);

        public static Plugin Plugin => _plugin;
        public static PluginContext Context => _context;

        private static Plugin _plugin;
        private static PluginContext _context;
        private static bool _initialized;

        public static void Initialize(Plugin plugin, PluginContext context)
        {
            lock (Sync)
            {
                _plugin = plugin;
                _context = context;
                RebuildInternal();
            }
        }

        public static void Rebuild()
        {
            lock (Sync)
            {
                if (_plugin == null || _context == null)
                    return;
                RebuildInternal();
            }
        }

        public static void RegisterModuleRootCommands(string moduleId, params Func<CommandExtension>[] factories)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(moduleId));

            lock (Sync)
            {
                ModuleRootCommands[moduleId] = factories ?? Array.Empty<Func<CommandExtension>>();
            }
        }

        public static void RegisterModuleRegionSubCommands(string moduleId, params Func<ISubCommand>[] factories)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(moduleId));

            lock (Sync)
            {
                ModuleRegionSubCommands[moduleId] = factories ?? Array.Empty<Func<ISubCommand>>();
            }
        }

        public static void RegisterModuleRegionOwnSubCommands(string moduleId, params Func<ISubCommand>[] factories)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(moduleId));

            lock (Sync)
            {
                ModuleRegionOwnSubCommands[moduleId] = factories ?? Array.Empty<Func<ISubCommand>>();
            }
        }

        public static void UnregisterModule(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return;

            lock (Sync)
            {
                ModuleRootCommands.Remove(moduleId);
                ModuleRegionSubCommands.Remove(moduleId);
                ModuleRegionOwnSubCommands.Remove(moduleId);
            }
        }

        public static ISubCommand[] GetModuleRegionSubCommands()
        {
            lock (Sync)
            {
                return ModuleRegionSubCommands.Values
                    .SelectMany(static list => list)
                    .Select(factory => factory())
                    .ToArray();
            }
        }

        public static ISubCommand[] GetModuleRegionOwnSubCommands()
        {
            lock (Sync)
            {
                return ModuleRegionOwnSubCommands.Values
                    .SelectMany(static list => list)
                    .Select(factory => factory())
                    .ToArray();
            }
        }

        public static void InitializeCommands(this Plugin plugin, PluginContext context, params CommandExtension[] commands)
        {
            CommandsInitializer.InitializeCommands(plugin, context, commands);
        }

        public static void Dispose()
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    CommandsInitializer.Dispose();
                    _initialized = false;
                }

                _plugin = null;
                _context = null;
            }
        }

        private static void RebuildInternal()
        {
            if (_initialized)
                CommandsInitializer.Dispose();

            var baseCommands = new List<CommandExtension>
            {
                new RegionExtensionCommand(),
                new RegionOwnCommand(),
                new RegionHistoryCommand()
            };

            baseCommands.AddRange(ModuleRootCommands.Values
                .SelectMany(static list => list)
                .Select(factory => factory()));

            InitializeCommands(_plugin, _context, baseCommands.ToArray());
            _initialized = true;
        }
    }
}

