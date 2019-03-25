using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class LoginAsUser : IDispatcher
    {
        ResourceManager Resource { get; }
        AppSettingsConfig AppSettings { get; }
        OrgSettingsConfig OrgSettings { get; }

        public LoginAsUser(ResourceManager resource, AppSettingsConfig appSettings, OrgSettingsConfig orgSettings)
        {
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
        }

        public Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)payload?["instanceUrl"] ?? "";
            var id = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
            var userId = (string)payload?["userId"] ?? "";
            if (userId == "") userId = id.Split('/').LastOrDefault();
            var page = (string)payload?["page"] ?? "";
            if (page == "") page = "/";
            var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
            var targetUrl = Resource.GetLoginUrlAs(instanceUrl, id, userId, page);
            var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
            Resource.OpenIncognitoBrowser(urlWithAccessCode, AppSettings.GetString(AppConstants.PATH_CHROME));
            return Task.FromResult<JToken>(null);
        }
    }
}