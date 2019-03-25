using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class ViewDownloadExports : IDispatcher
    {
        ResourceManager Resource { get;  }
        AppSettingsConfig AppSettings { get; }
        OrgSettingsConfig OrgSettings { get; }

        public ViewDownloadExports(ResourceManager resource, AppSettingsConfig appSettings, OrgSettingsConfig orgSettings)
        {
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
        }

        public Task<JToken> DispatchAsync(JToken payload)
        {
            var exportPath = (string)payload?["exportPath"] ?? "";
            var instanceUrl = (string)payload?["instanceUrl"] ?? "";
            var userId = (string)payload?["userId"] ?? "";

            var id = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
            var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
            var redirectUri = Resource.GetRedirectUrlByLoginUrl(id);
            var targeturl = string.IsNullOrEmpty(userId) ?
                instanceUrl + "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport" :
                Resource.GetLoginUrlAs(instanceUrl, id, userId,
                "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport");
            var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, targeturl);
            Resource.OpenIncognitoBrowser(urlWithAccessCode, AppSettings.GetString(AppConstants.PATH_CHROME));
            return Task.FromResult<JToken>(null);
        }
    }
}