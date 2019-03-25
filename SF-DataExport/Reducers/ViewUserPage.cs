using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class ViewUserPage : IDispatcher
    {
        ResourceManager Resource { get; }
        AppSettingsConfig AppSettings { get; }
        OrgSettingsConfig OrgSettings { get; }

        public ViewUserPage(ResourceManager resource, AppSettingsConfig appSettings, OrgSettingsConfig orgSettings)
        {
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
        }

        public Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)payload["instanceUrl"] ?? "";
            var userId = (string)payload["userId"] ?? "";
            var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
            var targetUrl = instanceUrl + "/" + userId + "?noredirect=1";
            var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
            Resource.OpenIncognitoBrowser(urlWithAccessCode, AppSettings.GetString(AppConstants.PATH_CHROME));
            return Task.FromResult<JToken>(null);
        }
    }
}