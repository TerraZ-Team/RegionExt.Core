namespace RegionExtension
{
    public static class Permissions
    {
        public const string Main = "regionext";
        public static readonly string RegionExtCmd = TShockAPI.Permissions.manageregion;
        public const string RegionOwnCmd = Main + ".own";
        public const string RegionHistoryCmd = Main + ".history";

        public static string[] GetAllPermissions() =>
            new[] { RegionExtCmd, RegionOwnCmd, RegionHistoryCmd };
    }
}

