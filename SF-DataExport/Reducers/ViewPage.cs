using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class ViewPage : IDispatcher
    {
        AppStore Store { get; }
        ResourceManager Resource { get; }
        AppSettingsConfig AppSettings { get; }
        OrgSettingsConfig OrgSettings { get; }

        public ViewPage(AppStore store, ResourceManager resource, AppSettingsConfig appSettings, OrgSettingsConfig orgSettings)
        {
            Store = store;
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
        }

        public Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)Store.GetState("currentInstanceUrl") ?? "";
            var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
            var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, (string)payload);
            Resource.OpenIncognitoBrowser(urlWithAccessCode, AppSettings.GetString(AppConstants.PATH_CHROME));
            return Task.FromResult<JToken>(null);
        }
    }
}