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
    public class SwitchUser : IDispatcher
    {
        AppStateManager AppState { get; }
        ResourceManager Resource { get; }
        OrgSettingsConfig OrgSettings { get; }

        public SwitchUser(AppStateManager appState, ResourceManager resource, OrgSettingsConfig orgSettings)
        {
            AppState = AppState;
            Resource = resource;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            try
            {
                AppState.Commit(new JObject { ["isLoading"] = true });

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
                        AppState.Commit(new JObject
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
                AppState.Commit(new JObject { ["isLoading"] = false });
            }
            return null;
        }
    }
}