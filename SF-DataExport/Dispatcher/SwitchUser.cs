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

                await resource.RunClientAsUserAsync((httpClient, cookieContainer, htmlContent) =>
                {
                    var cookies = cookieContainer.GetCookies(new Uri(instanceUrl));
                    var newAccessToken = cookies["sid"]?.Value ?? "";
                    if (newAccessToken != "")
                    
                    {
                        var newId = id.Remove(id.LastIndexOf('/') + 1) + userId;
                        appState.Commit(new JObject
                        {
                            ["currentAccessToken"] = newAccessToken,
                            ["currentId"] = newId,
                            ["userIdAs"] = userId,
                        });
                    }
                    return Task.FromResult(0);
                }, instanceUrl, accessToken, targetUrl, id, userId).GoOn();
            })
            .Finally(() => appState.Commit(new JObject { ["isLoading"] = false }))
            .ScheduleTask();
        }
    }
}