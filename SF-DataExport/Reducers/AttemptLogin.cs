using DotNetForce;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport.Reducers
{
    public class AttemptLogin : IDispatcher
    {
        AppStore Store { get; set; }
        ResourceManager Resource { get; set; }
        OrgSettingsConfig OrgSettings { get; set; }

        public AttemptLogin(AppStore store, ResourceManager resource, OrgSettingsConfig orgSettings)
        {
            Store = store;
            Resource = resource;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            try
            {
                Resource.ResetCookie();
                Store.Commit(new JObject { ["isLoading"] = true });
                var attemptingDomain = Regex.Replace((string)payload ?? "", "^https?://", "");

                if (attemptingDomain?.Length > 0)
                {
                    var loginUrl = "https://" + attemptingDomain;

                    if (attemptingDomain != "login.salesforce.com" && attemptingDomain != "test.salesforce.com")
                    {
                        var instanceUrl = "https://" + attemptingDomain;
                        var savedOrg = OrgSettings.Get(o => o[instanceUrl]);
                        if (savedOrg != null)
                        {
                            var accessToken = (string)savedOrg[OAuth.ACCESS_TOKEN] ?? "";
                            var refreshToken = (string)savedOrg[OAuth.REFRESH_TOKEN] ?? "";
                            loginUrl = Resource.GetLoginUrl(savedOrg[OAuth.ID]);
                            if (!Uri.IsWellFormedUriString(loginUrl, UriKind.Absolute))
                            {
                                loginUrl = "https://login.salesforce.com";
                            }
                            var client = new DNFClient(instanceUrl, accessToken, refreshToken);

                            try
                            {
                                await client.TokenRefreshAsync(new Uri(loginUrl), Resource.GetClientIdByLoginUrl(loginUrl)).GoOn();
                                await Store.SetOrganizationAsync(
                                    client.AccessToken,
                                    client.InstanceUrl,
                                    client.Id,
                                    client.RefreshToken).GoOn();
                                await Store.SetCurrentInstanceUrlAync(client).GoOn();
                                await Resource.GetCookieAsync(client.InstanceUrl, client.AccessToken).GoOn();
                                return null;
                            }
                            catch (IOException ioEx)
                            {
                                Store.Commit(new JObject
                                {
                                    ["alertMessage"] = ioEx.Message
                                });
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                                //appState.Commit(new JObject { ["alertMessage"] = ex.Message });
                                //return;
                            }
                        }
                    }

                    var targetUrl = loginUrl + "/services/oauth2/authorize" +
                        "?response_type=token" +
                        "&client_id=" + HttpUtility.UrlEncode(Resource.GetClientIdByLoginUrl(loginUrl)) +
                        "&redirect_uri=" + HttpUtility.UrlEncode(Resource.GetRedirectUrlByLoginUrl(loginUrl)) +
                        "&state=" + HttpUtility.UrlEncode(loginUrl) +
                        "&display=popup";
                    Store.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = targetUrl });
                }
            }
            finally
            {
                Store.Commit(new JObject { ["isLoading"] = false });
            }
            return null;
        }
    }
}