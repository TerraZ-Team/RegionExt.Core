using System;

namespace RegionExtension.Database
{
    internal static class DateTimeCodec
    {
        public static long ToUnixMilliseconds(DateTime value) =>
            value.ToUniversalTime().ToUnixTimeMilliseconds();

        public static DateTime FromUnixMilliseconds(long value) =>
            DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime;
    }

    internal static class DateTimeExtensions
    {
        public static long ToUnixTimeMilliseconds(this DateTime value)
        {
            var utc = value.ToUniversalTime();
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(utc - epoch).TotalMilliseconds;
        }
    }
}
