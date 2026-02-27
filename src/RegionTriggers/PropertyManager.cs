using RegionExtension.Commands.Parameters;
using RegionExtension.Database;
using RegionExtension.Database.EventsArgs;
using RegionExtension.RegionTriggers.Conditions;
using RegionExtension.RegionTriggers.RegionProperties;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

namespace RegionExtension.RegionTriggers
{
    public class PropertyManager
    {
        private readonly IRegionProperty[] _regionProperties;
        private readonly DatabaseTable<RegionPropertyDBUnit> _database;

        public IRegionProperty[] RegionProperties { get { return _regionProperties; } }

        public PropertyManager(IDbConnection dbConnection, TerrariaPlugin plugin, PluginContext context)
        {
            _regionProperties = CreateProperties(context);
            _database = new DatabaseTable<RegionPropertyDBUnit>("RegionProperties", dbConnection);
            Initialize(plugin);
        }

        public void Initialize(TerrariaPlugin plugin)
        {
            _database.InitializeTable();
            foreach (var prop in _regionProperties)
                prop.InitializeEventHandler(plugin);
            LoadProperties();
        }

        public void HandleRegionDeleted(Region region) => RemoveAllProperties(region);

        public IRegionProperty GetProperty(string name) =>
            _regionProperties.FirstOrDefault(p => p.Names.Contains(name.ToLower()));

        public ICommandParam[] GetPropertyParams(string propertyName) =>
            _regionProperties.FirstOrDefault(p => p.Names.Contains(propertyName.ToLower()))?.CommandParams;

        public bool AddRegionProps(Region region, string propertyName, ICommandParam[] commandParams)
        {
            var prop = GetRequiredProperty(propertyName);
            EnsurePropertyRowExists(region, prop);
            prop.AddRegionProperties(region, commandParams);
            return SavePropertyState(region, prop);
        }

        public bool RemoveRegionProperties(Region region, string propertyName, ICommandParam[] commandParams)
        {
            var prop = GetRequiredProperty(propertyName);
            if (!IsRegionDefined(prop, region.ID))
                return false;
            var currentRegion = ResolveRegionReference(prop, region);
            prop.RemoveRegionProperties(currentRegion, commandParams);
            if (!IsRegionDefined(prop, region.ID))
                return RemovePropertyState(region, prop);
            return SavePropertyState(region, prop);
        }

        public bool AddRegionCondition(Region region, string propertyName, ICommandParam[] commandParams, IRegionCondition regionCondition)
        {
            var prop = GetRequiredProperty(propertyName);
            EnsurePropertyRowExists(region, prop);
            prop.AddCondition(region, commandParams, regionCondition);
            return SavePropertyState(region, prop);
        }

        public bool RemoveRegionCondition(Region region, string propertyName, ICommandParam[] commandParams, IRegionCondition regionCondition)
        {
            var prop = GetRequiredProperty(propertyName);
            EnsurePropertyRowExists(region, prop);
            prop.RemoveCondition(region, commandParams, regionCondition);
            return SavePropertyState(region, prop);
        }

        public void ClearProperty(Region region, string propertyName)
        {
            var prop = GetRequiredProperty(propertyName);
            if (!IsRegionDefined(prop, region.ID))
                return;
            prop.ClearProperties(ResolveRegionReference(prop, region));
            RemovePropertyState(region, prop);
        }

        public bool RemoveAllProperties(Region region)
        {
            foreach (var item in _regionProperties.Where(p => IsRegionDefined(p, region.ID)))
                item.ClearProperties(ResolveRegionReference(item, region));
            return _database.RemoveByColumn(new[] { (nameof(RegionPropertyDBUnit.RegionId), (object)region.ID) });
        }

        public void Reload(ReloadEventArgs e)
        {
            foreach (var property in _regionProperties)
                foreach (var region in property.DefinedRegions)
                    property.ClearProperties(region);
            LoadProperties();
        }

        public void Dispose(TerrariaPlugin plugin)
        {
            foreach (var property in _regionProperties)
                property.Dispose(plugin);
        }

        private void LoadProperties()
        {
            foreach (var region in TShock.Regions.Regions)
            {
                var list = _database.GetValues(RegionPropertyDBUnit.Reader, new[] { (nameof(RegionPropertyDBUnit.RegionId), (object)region.ID) }).Select(p => (p.PropertyName, p.Conditions, p.Args));
                foreach (var propInfo in list)
                {
                    if (string.IsNullOrEmpty(propInfo.Args))
                    {
                        _database.RemoveByColumn(new[] { (nameof(RegionPropertyDBUnit.RegionId), (object)region.ID), (nameof(RegionPropertyDBUnit.PropertyName), (object)propInfo.PropertyName) });
                        continue;
                    }
                    _regionProperties.FirstOrDefault(p => p.Names[0].Equals(propInfo.PropertyName))
                                     ?.SetFromString(region, new(propInfo.Conditions, propInfo.Args));
                }
            }
        }

        private IRegionProperty GetRequiredProperty(string propertyName) =>
            _regionProperties.First(p => p.Names.Contains(propertyName.ToLower()));

        private void EnsurePropertyRowExists(Region region, IRegionProperty property)
        {
            if (!IsRegionDefined(property, region.ID))
                _database.SaveValue(new RegionPropertyDBUnit(region.ID, property.Names[0], ""));
        }

        private bool SavePropertyState(Region region, IRegionProperty property)
        {
            var currentRegion = ResolveRegionReference(property, region);
            var pair = property.GetStringArgs(currentRegion);
            var conditions = GetPropertyConditions(region, property);
            return _database.UpdateByColumn(nameof(RegionPropertyDBUnit.Args), pair.Args, conditions) &&
                   _database.UpdateByColumn(nameof(RegionPropertyDBUnit.Conditions), pair.Conditions, conditions);
        }

        private bool RemovePropertyState(Region region, IRegionProperty property) =>
            _database.RemoveByColumn(GetPropertyConditions(region, property));

        private static (string columnName, object value)[] GetPropertyConditions(Region region, IRegionProperty property) =>
            new[]
            {
                (nameof(RegionPropertyDBUnit.RegionId), (object)region.ID),
                (nameof(RegionPropertyDBUnit.PropertyName), (object)property.Names[0])
            };

        private static IRegionProperty[] CreateProperties(PluginContext context) =>
            new IRegionProperty[]
            {
                new AlwaysPvp(),
                new BanHostile(),
                new BlockDoorToggle(),
                new BlockTileFrame(),
                new ClearItems(),
                new MaxSpawnRewrite(),
                new NoPvp(),
                new NPCSpawnRewrite(),
                new RegionExtension.RegionTriggers.RegionProperties.ProjectileBan(),
                new RegionItemBan(context.TriggerIgnores)
            };

        private static bool IsRegionDefined(IRegionProperty property, int regionId) =>
            property.DefinedRegions.Any(r => r.ID == regionId);

        private static Region ResolveRegionReference(IRegionProperty property, Region region) =>
            property.DefinedRegions.FirstOrDefault(r => r.ID == region.ID) ?? region;
    }
}

