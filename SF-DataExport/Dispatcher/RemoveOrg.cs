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
    public class RemoveOrg
    {
        public JToken Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings)
        {
            Rx.FromAsync(async () =>
            {
                var instanceUrl = payload?.ToString() ?? "";
                var loginUrl = resource.GetLoginUrl(orgSettings.Get(o => o[instanceUrl][OAuth.ID]));
                await orgSettings.SaveAysnc(json =>
                    {
                        if (json[instanceUrl] != null)
                        {
                            json.Remove(instanceUrl);
                        }
                    }).Continue();
                appState.Commit(appState.GetOrgSettings());

                if (appState.Value["currentInstanceUrl"]?.ToString() == instanceUrl)
                {
                    appState.Commit(new JObject
                    {
                        ["currentInstanceUrl"] = "",
                        ["exportUserId"] = "",
                        ["popoverUserId"] = "",
                        ["showOrgModal"] = true,
                        ["userDisplayName"] = "",
                        ["userEmail"] = "",
                        ["userId"] = "",
                        ["userName"] = "",
                        ["userPhoto"] = "",
                        ["users"] = new JArray()
                    });
                }
                var oauthPage = instanceUrl +
                    "/identity/app/connectedAppsUserList.apexp?app_name=SFDataExport&consumer_key=" +
                    HttpUtility.UrlEncode(resource.GetClientIdByLoginUrl(loginUrl));
                resource.OpenBrowserIncognito(oauthPage, appSettings.GetString(AppConstants.CHROME_PATH));
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}