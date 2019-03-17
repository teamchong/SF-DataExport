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
    public class RemoveOrg
    {
        public void Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings)
        {
            Observable.FromAsync(async () =>
            {
                var instanceUrl = (string)payload ?? "";
                var loginUrl = resource.GetLoginUrl(orgSettings.Get(o => o[instanceUrl][OAuth.ID]));
                await orgSettings.SaveAysnc(json =>
                    {
                        if (json[instanceUrl] != null)
                        {
                            json.Remove(instanceUrl);
                        }
                    }).GoOn();
                appState.Commit(appState.GetOrgSettings());

                if ((string)appState.Value["currentInstanceUrl"] == instanceUrl)
                {
                    appState.Commit(new JObject
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
                    HttpUtility.UrlEncode(resource.GetClientIdByLoginUrl(loginUrl));
                resource.OpenIncognitoBrowser(oauthPage, appSettings.GetString(AppConstants.PATH_CHROME));
            })
            .ScheduleTask();
        }
    }
}