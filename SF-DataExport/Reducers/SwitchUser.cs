using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class SwitchUser : IDispatcher
    {
        AppStore Store { get; }
        ResourceManager Resource { get; }
        OrgSettingsConfig OrgSettings { get; }

        public SwitchUser(AppStore store, ResourceManager resource, OrgSettingsConfig orgSettings)
        {
            Store = store;
            Resource = resource;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            try
            {
                Store.Commit(new JObject { ["isLoading"] = true });

                var instanceUrl = (string)payload?["instanceUrl"] ?? "";
                var userId = (string)payload?["userId"] ?? "";
                var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
                var refreshToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.REFRESH_TOKEN]) ?? "";
                var id = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
                var targetUrl = Resource.GetLoginUrlAs(instanceUrl, id, userId, "/");

                await Resource.RunClientAsUserAsync((httpClient, cookieContainer, htmlContent) =>
                {
                    var cookies = cookieContainer.GetCookies(new Uri(instanceUrl));
                    var newAccessToken = cookies["sid"]?.Value ?? "";
                    if (newAccessToken != "")

                    {
                        var newId = id.Remove(id.LastIndexOf('/') + 1) + userId;
                        Store.Commit(new JObject
                        {
                            ["currentAccessToken"] = newAccessToken,
                            ["currentId"] = newId,
                            ["userIdAs"] = userId,
                        });
                    }
                    return Task.FromResult(0);
                }, instanceUrl, accessToken, targetUrl, id, userId).GoOn();
            }
            finally
            {
                Store.Commit(new JObject { ["isLoading"] = false });
            }
            return null;
        }
    }
}