using System;

namespace RegionExtension
{
    public static class RequestConfigResolver
    {
        private static Func<ConfigFile> _provider;

        public static void SetProvider(Func<ConfigFile> provider)
        {
            _provider = provider;
        }

        public static ConfigFile Resolve(ConfigFile fallback)
        {
            var provided = _provider?.Invoke();
            return provided ?? fallback;
        }
    }
}
