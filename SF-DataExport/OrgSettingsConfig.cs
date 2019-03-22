using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
