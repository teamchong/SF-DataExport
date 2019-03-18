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
    public class AttemptLogin
    {
        public void Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig orgSettings)
        {
            appState.Commit(new JObject { ["isLoading"] = true });
            Observable.FromAsync(async () =>
            {
                var attemptingDomain = Regex.Replace((string)payload ?? "", "^https?://", "");

                if (!string.IsNullOrEmpty(attemptingDomain))
                {
                    var loginUrl = "https://" + attemptingDomain;

                    if (attemptingDomain != "login.salesforce.com" && attemptingDomain != "test.salesforce.com")
                    {
                        var instanceUrl = "https://" + attemptingDomain;
                        var savedOrg = orgSettings.Get(o => o[instanceUrl]);
                        if (savedOrg != null)
                        {
                            var accessToken = (string)savedOrg[OAuth.ACCESS_TOKEN] ?? "";
                            var refreshToken = (string)savedOrg[OAuth.REFRESH_TOKEN] ?? "";
                            loginUrl = resource.GetLoginUrl(savedOrg[OAuth.ID]);
                            if (!Uri.IsWellFormedUriString(loginUrl, UriKind.Absolute))
                            {
                                loginUrl = "https://login.salesforce.com";
                            }
                            var client = new DNFClient(instanceUrl, accessToken, refreshToken);

                            try
                            {
                                await client.TokenRefreshAsync(new Uri(loginUrl), resource.GetClientIdByLoginUrl(loginUrl)).GoOn();
                                await appState.SetOrganizationAsync(
                                    client.AccessToken,
                                    client.InstanceUrl,
                                    client.Id,
                                    client.RefreshToken).GoOn();
                                appState.SetCurrentInstanceUrl(client);
                                resource.ResetCookie();
                                await resource.GetCookieAsync(client.InstanceUrl, client.AccessToken).GoOn();
                                return;
                            }
                            catch (IOException ioEx)
                            {
                                appState.Commit(new JObject
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
                        "&client_id=" + HttpUtility.UrlEncode(resource.GetClientIdByLoginUrl(loginUrl)) +
                        "&redirect_uri=" + HttpUtility.UrlEncode(resource.GetRedirectUrlByLoginUrl(loginUrl)) +
                        "&state=" + HttpUtility.UrlEncode(loginUrl) +
                        "&display=popup";
                    appState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = targetUrl });
                    return;
                }
            })
            .Finally(() => appState.Commit(new JObject { ["isLoading"] = false }))
            .ScheduleTask();
        }
    }
}