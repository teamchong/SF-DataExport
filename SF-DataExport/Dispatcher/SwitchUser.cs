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
    public class SwitchUser
    {
        public void Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings)
        {
            Observable.FromAsync(async () =>
            {
                appState.Commit(new JObject { ["isLoading"] = true });
                var instanceUrl = (string)payload?["instanceUrl"] ?? "";
                var userId = (string)payload?["userId"] ?? "";
                var accessToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
                var refreshToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.REFRESH_TOKEN]) ?? "";
                var id = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
                var targetUrl = resource.GetLoginUrlAs(instanceUrl, id, userId, "/");
                var urlWithAccessCode = resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);

                var cookieContainer = new CookieContainer();
                using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                {
                    using (var httpClient = new HttpClient(handler))
                    {
                        var domain = new Uri(instanceUrl).Host;
                        var htmlContent = await httpClient.GetStringAsync(urlWithAccessCode).GoOn();
                        htmlContent = await resource.WaitForRedirectAsync(httpClient, instanceUrl, htmlContent, targetUrl).GoOn();
                        var cookies = cookieContainer.GetCookies(new Uri(instanceUrl));
                        var newAccessToken = cookies["sid"]?.Value ?? "";
                        if (newAccessToken != "")
                        {
                            var newId = id.Remove(id.LastIndexOf('/') + 1) + userId;
                            appState.Commit(new JObject
                            {
                                ["currentAccessToken"] = newAccessToken,
                                ["currentId"] = newId,
                                ["userDisplayName"] = "",
                                ["userEmail"] = "",
                                ["userId"] = userId,
                                ["userName"] = "",
                                ["userPicture"] = "",
                                ["userThumbnail"] = "",
                            });
                            var client = new DNFClient(instanceUrl, accessToken, refreshToken);
                            Observable.FromAsync(() => client.UserInfo(newId))
                            .SelectMany(userInfo => Observable.Merge(

                                Observable.If(() => (string)appState.Value["currentInstanceUrl"] == client.InstanceUrl && (string)appState.Value["userId"] == userId,
                                    Observable.Start(() => appState.Commit(new JObject
                                    {
                                        ["userDisplayName"] = userInfo?["display_name"],
                                        ["userEmail"] = userInfo?["email"],
                                        ["userName"] = userInfo?["username"],
                                    }))
                                    .Catch(Observable.Empty<Unit>()),
                                    Observable.Empty<Unit>()
                                ),
                                
                                Observable.FromAsync(() => resource.GetDataUriViaAccessToken(client.InstanceUrl, client.AccessToken,
                                    (string)userInfo?["photos"]?["picture"], "image/png"))
                                .SelectMany(userPhoto =>
                                    Observable.If(() => (string)appState.Value["currentInstanceUrl"] == client.InstanceUrl && (string)appState.Value["userId"] == userId,
                                        Observable.Start(() => appState.Commit(new JObject { ["userPicture"] = userPhoto })),
                                        Observable.Throw<Unit>(new InvalidOperationException())
                                    )
                                ),

                                Observable.FromAsync(() => resource.GetDataUriViaAccessToken(client.InstanceUrl, client.AccessToken,
                                    (string)userInfo?["photos"]?["thumbnail"], "image/png"))
                                .SelectMany(userPhoto =>
                                    Observable.If(() => (string)appState.Value["currentInstanceUrl"] == client.InstanceUrl && (string)appState.Value["userId"] == userId,
                                        Observable.Start(() => appState.Commit(new JObject { ["userThumbnail"] = userPhoto })),
                                        Observable.Throw<Unit>(new InvalidOperationException())
                                    )
                                )
                            )).ScheduleTask();
                        }
                    }
                }
            })
            .Finally(() => appState.Commit(new JObject { ["isLoading"] = false }))
            .ScheduleTask();
        }
    }
}