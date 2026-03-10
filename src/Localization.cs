using System;
using System.Collections.Generic;
using TShockAPI;

namespace RegionExtension
{
    public static class Localization
    {
        private static readonly Dictionary<string, Dictionary<string, string>> RegisteredModuleLanguages = new(StringComparer.OrdinalIgnoreCase);

        public static string[] PlayersLocalization = new string[256];
        public static string DefaultLocalization { get; set; } = "EN";

        public static Dictionary<string, Dictionary<string, string>> Languages { get; } = CreateCoreLanguages();

        public static void RegisterModuleLanguages(string moduleId, Dictionary<string, Dictionary<string, string>> moduleLanguages)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(moduleId));

            UnregisterModuleLanguages(moduleId);
            if (moduleLanguages == null)
                return;

            var registeredKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var languagePair in moduleLanguages)
            {
                if (!Languages.TryGetValue(languagePair.Key, out var language))
                {
                    language = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    Languages[languagePair.Key] = language;
                }

                foreach (var entry in languagePair.Value)
                {
                    language[entry.Key] = entry.Value;
                    registeredKeys[$"{languagePair.Key}:{entry.Key}"] = entry.Key;
                }
            }

            RegisteredModuleLanguages[moduleId] = registeredKeys;
        }

        public static void UnregisterModuleLanguages(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId) || !RegisteredModuleLanguages.Remove(moduleId, out var registeredKeys))
                return;

            foreach (var compositeKey in registeredKeys.Keys)
            {
                var separator = compositeKey.IndexOf(':');
                if (separator <= 0)
                    continue;

                var languageKey = compositeKey[..separator];
                var resourceKey = compositeKey[(separator + 1)..];
                if (Languages.TryGetValue(languageKey, out var language))
                    language.Remove(resourceKey);
            }
        }

        public static string GetStringForPlayer(string name, TSPlayer player = null)
        {
            var localization = "EN";
            if (player == null || player.TPlayer.whoAmI == -1 || string.IsNullOrEmpty(PlayersLocalization[player.TPlayer.whoAmI]))
                localization = DefaultLocalization;
            else
                localization = PlayersLocalization[player.TPlayer.whoAmI];

            if (!Languages.TryGetValue(localization, out var values))
                values = Languages["EN"];

            if (!values.TryGetValue(name, out var localizedValue))
            {
                if (!string.Equals(localization, "EN", StringComparison.OrdinalIgnoreCase) &&
                    Languages["EN"].TryGetValue(name, out var englishValue))
                    return englishValue;

                return name;
            }

            return localizedValue;
        }

        private static Dictionary<string, Dictionary<string, string>> CreateCoreLanguages() =>
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["EN"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["AllowGroupDesc"] = "Allows a user group to a region.",
                    ["AllowUserDesc"] = "Allows a user to a region.",
                    ["ClearMembersDesc"] = "Remove all members from region.",
                    ["ClearPointDesc"] = "Clears the temporary region points.",
                    ["DefineRegionDesc"] = "Defines the region with the given name.",
                    ["ListDeletedRegionsDesc"] = "Get list of deleted regions.",
                    ["DeleteRegionDesc"] = "Deletes the given region.",
                    ["BreakFRRequestDesc"] = "Breaks fast region request.",
                    ["FastRegionDesc"] = "Create new region with two given point and params.",
                    ["GetHistoryDesc"] = "Gets history about region.",
                    ["GetRegionNameDesc"] = "Shows the name of the region at the given point. Additional params -u, -z, -p",
                    ["LastActiveDesc"] = "Get list of last active regions.",
                    ["MoveRegionDesc"] = "Move region with given name in given direction.",
                    ["OwnerListDesc"] = "Get list of regions in which the given player is owner",
                    ["RedoHistoryDesc"] = "Redo actions on region.",
                    ["RegionInfoDesc"] = "Displays several information about the given region.",
                    ["RegionListDesc"] = "Lists all regions.",
                    ["RegionRemoveGroupDesc"] = "Removes a user group from a region.",
                    ["RegionRemoveDesc"] = "Removes a user from a region.",
                    ["RenameRegionDesc"] = "Renames the given region.",
                    ["RegionResizeDesc"] = "Resizes a region.",
                    ["RegionRestoreByUserDesc"] = "Restore region from deleted regions with user.",
                    ["RegionRestoreDesc"] = "Restore region from deleted regions.",
                    ["SelfOwnerDesc"] = "Get list of regions in which you is owner.",
                    ["SetOwnerDesc"] = "Set region owner.",
                    ["SetProtectDesc"] = "Sets whether the tiles inside the region are protected or not.",
                    ["SetRegionPointDesc"] = "Sets the temporary region points.",
                    ["SetRegionZDesc"] = "Sets the z-order of the region.",
                    ["TeleportToRegionDesc"] = "Teleports you to the given region's center.",
                    ["UndoHistoryDesc"] = "Undo actions on region.",
                    ["AllowedListDesc"] = "Returns all regions with allowed user.",
                    ["HelpCommandDesc"] = "Returns all info about this command.",
                    ["HelpSubCommandDesc"] = "Returns all info about this sub-command."
                },
                ["RU"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["AllowGroupDesc"] = "Добавляет группу в регион.",
                    ["AllowUserDesc"] = "Добавляет игрока в регион.",
                    ["ClearMembersDesc"] = "Удаляет всех игроков из региона.",
                    ["ClearPointDesc"] = "Сбрасывает установленные точки для задания региона.",
                    ["DefineRegionDesc"] = "Создает регион с установленными точками и заданным именем.",
                    ["ListDeletedRegionsDesc"] = "Перечисляет все удаленные регионы.",
                    ["DeleteRegionDesc"] = "Удаляет заданный регион.",
                    ["BreakFRRequestDesc"] = "Удаляет запрос на быстрый регион.",
                    ["FastRegionDesc"] = "Создает регион и запрашивает установление точек.",
                    ["GetHistoryDesc"] = "Получает всю историю действий на регион.",
                    ["GetRegionNameDesc"] = "Отображает имя региона в заданой точке. Дополнительные флаги -u, -z, -p",
                    ["LastActiveDesc"] = "Перечисляет все регионы в порядке активности.",
                    ["MoveRegionDesc"] = "Перемещает регион в заданном направлении.",
                    ["OwnerListDesc"] = "Перечисляет все регионы с заданным владельцем.",
                    ["RedoHistoryDesc"] = "Восстанавливает действие над регионом.",
                    ["RegionInfoDesc"] = "Отображает информацию о регионе.",
                    ["RegionListDesc"] = "Перечисляет все доступные регионы.",
                    ["RegionRemoveGroupDesc"] = "Удаляет группу из региона.",
                    ["RegionRemoveDesc"] = "Удаляет пользователя из региона.",
                    ["RenameRegionDesc"] = "Переименовывает заданный регион.",
                    ["RegionResizeDesc"] = "Изменяет размер региона.",
                    ["RegionRestoreByUserDesc"] = "Восстанавливает регионы из удаленных по пользователю.",
                    ["RegionRestoreDesc"] = "Восстанавливает регион из удаленных.",
                    ["SelfOwnerDesc"] = "Отображает все регионы, в которых вы владелец.",
                    ["SetOwnerDesc"] = "Устанавливает владельца региона.",
                    ["SetProtectDesc"] = "Устанавливает защищенность региона. true - нельзя менять блоки false - можно",
                    ["SetRegionPointDesc"] = "Устанавливает точку для создания региона.",
                    ["SetRegionZDesc"] = "Устанавливает z-приоритет региона.",
                    ["TeleportToRegionDesc"] = "Телепортирует в центр заданного региона.",
                    ["UndoHistoryDesc"] = "Отменяет действие над регионом.",
                    ["AllowedListDesc"] = "Отображает все регионы, где данный пользователь добавлен.",
                    ["HelpCommandDesc"] = "Отображает всю информацию о данной команде.",
                    ["HelpSubCommandDesc"] = "Отображает всю информацию о данной под-команде."
                }
            };
    }
}
