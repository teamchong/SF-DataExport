using System.IO;

namespace SF_DataExport
{
    public class OrgSettingsConfig : JsonConfig
    {
        public OrgSettingsConfig(ResourceManager resource, AppSettingsConfig appSettings)
            : base(Path.Combine(resource.GetOrgPath(appSettings).orgPath, AppConstants.JSON_ORG_SETTINGS))
        {
        }
    }
}
