using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport.Reducers
{
    public class RemoveOrg : IDispatcher
    {
        AppStore Store { get; }
        ResourceManager Resource { get; }
        AppSettingsConfig AppSettings { get; }
        OrgSettingsConfig OrgSettings { get; }

        public RemoveOrg(AppStore store, ResourceManager resource, AppSettingsConfig appSettings, OrgSettingsConfig orgSettings)
        {
            Store = store;
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)payload ?? "";
            var loginUrl = Resource.GetLoginUrl(OrgSettings.Get(o => o[instanceUrl][OAuth.ID]));
            await OrgSettings.SaveAysnc(json =>
                {
                    if (json[instanceUrl] != null)
                    {
                        json.Remove(instanceUrl);
                    }
                }).GoOn();
            Store.Commit(Store.GetOrgSettings());

            if ((string)Store.GetState("currentInstanceUrl") == instanceUrl)
            {
                Store.Commit(new JObject
                {
                    ["currentInstanceUrl"] = "",
                    ["objects"] = new JArray(),
                    ["orgLimits"] = new JArray(),
                    ["orgLimitsLog"] = new JArray(),
                    ["popoverUserId"] = "",
                    ["showLimitsModal"] = false,
                    ["showOrgModal"] = true,
                    ["showPhotosModal"] = false,
                    ["toolingObjects"] = new JArray(),
                    ["userId"] = "",
                    ["userIdAs"] = "",
                    ["userProfiles"] = new JArray(),
                    ["userRoles"] = new JObject(),
                    ["users"] = new JArray(),
                });
            }
            var oauthPage = instanceUrl +
                "/identity/app/connectedAppsUserList.apexp?app_name=SFDataExport&consumer_key=" +
                HttpUtility.UrlEncode(Resource.GetClientIdByLoginUrl(loginUrl));
            Resource.OpenIncognitoBrowser(oauthPage, AppSettings.GetString(AppConstants.PATH_CHROME));
            return null;
        }
    }
}