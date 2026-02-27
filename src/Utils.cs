using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;

namespace RegionExtension
{
    public static class Utils
    {
        public const string ColorTagFormat = "[c/{0}:{1}]";

        public static string AutoCompleteSameName(string oldName, string format)
        {
            string newName = oldName;
            var reg = TShock.Regions.GetRegionByName(newName);
            for (int i = 1; reg != null; i++)
            {
                newName = string.Format(format, oldName, i);
                reg = TShock.Regions.GetRegionByName(newName);
            }
            return newName;
        }

        public static float CountDistance(float x1, float y1, float x2, float y2) =>
            (float)Math.Sqrt(Math.Pow(Math.Abs(x1 - x2), 2) + Math.Pow(Math.Abs(y1 - y2), 2));

        public static string DateFormat { get { return "dd.MM.yyyy HH:mm:ss UTC+0"; } }
        public static string ShortDateFormat { get { return "dd.MM"; } }

        public static string ColorCommand(string str) =>
            ColorTagFormat.SFormat("b3c9ff", str);
        public static string ColorRegion(string str) =>
            ColorTagFormat.SFormat("d6d160", str);
        public static string ColorDate(string str) =>
            ColorTagFormat.SFormat("5cb5a3", str);

        public static string GetGradientByPos(string str, double pos)
        {
            var firstClr = Color.White;
            var secondClr = Color.Red;
            int r = (int)Math.Floor(firstClr.R + (secondClr.R - firstClr.R) * pos);
            int g = (int)Math.Floor(firstClr.G + (secondClr.G - firstClr.G) * pos);
            int b = (int)Math.Floor(firstClr.B + (secondClr.B - firstClr.B) * pos);
            var hex = $"{r:X2}{g:X2}{b:X2}";
            if(pos < 0 || pos > 1)
            {
                r = 255;
                g = 255;
                b = 255;
            }
            if(str.Contains("]"))
            {
                var strs = str.Split(']');
                var res = string.Join($"[c/{hex}:]]", strs.Select(s => string.IsNullOrEmpty(s) ? "" : $"[c/{hex}:{s}]"));
                return res;
            }
            return $"[c/{hex}:{str}]";
        }

        public static string GetGradientByDateTime(string str, DateTime start, DateTime end)
        {
            var dateNow = DateTime.UtcNow;
            var pos = (dateNow - start).TotalSeconds / (end - start).TotalSeconds;
            return GetGradientByPos(str, pos);
        }

        public static bool TryAutoComplete(ConfigFile config, string str, out string result)
        {
            if (!config.AutoCompleteSameName)
            {
                result = str;
                return !TShock.Regions.Regions.Any(r => r.Name.ToLower().Equals(str.ToLower()));
            }
            int num = 0;
            string res = str;
            while (TShock.Regions.Regions.Any(r => r.Name.ToLower().Equals(res.ToLower())))
            {
                res = config.AutoCompleteSameNameFormat.SFormat(str, num);
                num++;
            }
            result = res;
            return true;
        }

        public static bool TryAutoComplete(ConfigFile config, string str, Rectangle regionArea, out string result)
        {
            int num = 0;
            var reg = TShock.Regions.Regions.FirstOrDefault(r => r.Name.ToLower().Equals(str.ToLower()));
            var res = str;
            while (reg != null)
            {
                if (reg.Area.Equals(regionArea))
                {
                    result = null;
                    return false;
                }
                res = config.AutoCompleteSameNameFormat.SFormat(res, num);
                reg = TShock.Regions.Regions.FirstOrDefault(r => r.Name.ToLower().Equals(res.ToLower()));
                num++;
            }
            result = res;
            return true;
        }
    }
}

