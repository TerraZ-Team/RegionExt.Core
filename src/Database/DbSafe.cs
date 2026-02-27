using System;
using TShockAPI;

namespace RegionExtension.Database
{
    internal static class DbSafe
    {
        public static bool Execute(string operationName, Func<bool> operation)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[RegionExt][DB] {operationName}: {ex.Message}");
                return false;
            }
        }

        public static void Execute(string operationName, Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[RegionExt][DB] {operationName}: {ex.Message}");
            }
        }

        public static T Read<T>(string operationName, Func<T> operation, T fallback = default)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[RegionExt][DB] {operationName}: {ex.Message}");
                return fallback;
            }
        }
    }
}
