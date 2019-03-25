using System.IO;

namespace SF_DataExport
{
    public class AppSettingsConfig : JsonConfig
    {
        public AppSettingsConfig(ResourceManager resource)
            : base(Path.Combine(resource.DefaultDirectory, AppConstants.JSON_APP_SETTINGS))
        {
        }
    }
}
