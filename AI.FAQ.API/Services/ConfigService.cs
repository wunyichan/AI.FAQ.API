namespace AI.FAQ.API.Services
{
    public static class ConfigService
    {
        public static string GetConfigValue(IConfiguration configuration, string key, string? defaultValue = "")
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!String.IsNullOrEmpty(defaultValue))
                    value = defaultValue;
                else
                    throw new ArgumentException($"Configuration value for key '{key}' is not set or is empty.");
            }
            return value;
        }
    }
}

