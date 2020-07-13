using System;

namespace SharedConfig
{
    public static class SharedConfig
    {
        public static readonly string EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        public static bool IsProduction => EnvironmentName == "Production";
        public static bool IsDevelopment => EnvironmentName == "Development";

        public static EnvironmentNameEnum EnvironmentNameEnum {
            get {
                return EnvironmentName switch
                {
                    "Production" => EnvironmentNameEnum.Production,
                    "Staging" => EnvironmentNameEnum.Staging,
                    "Development" => EnvironmentNameEnum.Development,
                    _ => EnvironmentNameEnum.Development
                };
            }
        }
    }
}