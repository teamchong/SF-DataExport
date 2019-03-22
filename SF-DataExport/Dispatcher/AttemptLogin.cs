using DotNetForce;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Unit = System.Reactive.Unit;

namespace SF_DataExport.Dispatcher
{
    public class AttemptLogin : IDispatcher
    {
        AppStateManager AppState { get; set; }
        ResourceManager Resource { get; set; }
        OrgSettingsConfig OrgSettings { get; set; }

        public AttemptLogin(AppStateManager appState, ResourceManager resource, OrgSettingsConfig orgSettings)
        {
            AppState = appState;
            Resource = resource;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            try
            {
                AppState.Commit(new JObject { ["isLoading"] = true });
                var attemptingDomain = Regex.Replace((string)payload ?? "", "^https?://", "");

                if (!string.IsNullOrEmpty(attemptingDomain))
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
                                await AppState.SetOrganizationAsync(
                                    client.AccessToken,
                                    client.InstanceUrl,
                                    client.Id,
                                    client.RefreshToken).GoOn();
                                AppState.SetCurrentInstanceUrl(client);
                                Resource.ResetCookie();
                                await Resource.GetCookieAsync(client.InstanceUrl, client.AccessToken).GoOn();
                                return null;
                            }
                            catch (IOException ioEx)
                            {
                                AppState.Commit(new JObject
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
                    AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = targetUrl });
                }
            }
            finally
            {
                AppState.Commit(new JObject { ["isLoading"] = false });
            }
            return null;
        }
    }
}