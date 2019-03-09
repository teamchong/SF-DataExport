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
using Rx = System.Reactive.Linq.Observable;
using Unit = System.Reactive.Unit;

namespace SF_DataExport.Dispatcher
{
    public class AttemptLogin
    {
        public JToken Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig orgSettings)
        {
            Rx.FromAsync(async () =>
            {
                var attemptingDomain = Regex.Replace(payload?.ToString() ?? "", "^https?://", "");

                if (!string.IsNullOrEmpty(attemptingDomain))
                {
                    var loginUrl = "https://" + attemptingDomain;

                    if (attemptingDomain != "login.salesforce.com" && attemptingDomain != "test.salesforce.com")
                    {
                        var instanceUrl = "https://" + attemptingDomain;
                        var savedOrg = orgSettings.Get(o => o[instanceUrl]);
                        if (savedOrg != null)
                        {
                            var accessToken = savedOrg[OAuth.ACCESS_TOKEN]?.ToString() ?? "";
                            var refreshToken = savedOrg[OAuth.REFRESH_TOKEN]?.ToString() ?? "";
                            loginUrl = resource.GetLoginUrl(savedOrg[OAuth.ID]);
                            if (!Uri.IsWellFormedUriString(loginUrl, UriKind.Absolute))
                            {
                                loginUrl = "https://login.salesforce.com";
                            }
                            var client = new DNFClient(instanceUrl, accessToken, refreshToken);

                            try
                            {
                                await client.TokenRefreshAsync(new Uri(loginUrl), resource.GetClientIdByLoginUrl(loginUrl))
                                    .Continue();
                                await appState.SetOrganizationAsync(
                                    client.AccessToken,
                                    client.InstanceUrl,
                                    client.Id,
                                    client.RefreshToken)
                                    .Continue();
                                appState.SetCurrentInstanceUrl(client);
                                return;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                                appState.PageAlert(ex.Message);
                            }
                        }
                    }

                    var targetUrl = loginUrl + "/services/oauth2/authorize" +
                        "?response_type=token" +
                        "&client_id=" + HttpUtility.UrlEncode(resource.GetClientIdByLoginUrl(loginUrl)) +
                        "&redirect_uri=" + HttpUtility.UrlEncode(resource.GetRedirectUrlByLoginUrl(loginUrl)) +
                        "&state=" + HttpUtility.UrlEncode(loginUrl) +
                        "&display=popup";
                    await appState.PageRedirectAsync(targetUrl).Continue();
                }
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}