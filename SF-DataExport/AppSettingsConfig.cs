using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
