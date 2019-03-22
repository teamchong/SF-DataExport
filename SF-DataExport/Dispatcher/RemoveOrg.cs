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
    public class RemoveOrg : IDispatcher
    {
        AppStateManager AppState { get; }
        ResourceManager Resource { get; }
        AppSettingsConfig AppSettings { get; }
        OrgSettingsConfig OrgSettings { get; }

        public RemoveOrg(AppStateManager appState, ResourceManager resource, AppSettingsConfig appSettings, OrgSettingsConfig orgSettings)
        {
            AppState = appState;
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)payload ?? "";
            var loginUrl = Resource.GetLoginUrl(OrgSettings.Get(o => o[instanceUrl][OAuth.ID]));
            await OrgSettings.SaveAysnc(json =>
                {
                    if (json[instanceUrl] != null)
                    {
                        json.Remove(instanceUrl);
                    }
                }).GoOn();
            AppState.Commit(AppState.GetOrgSettings());

            if ((string)AppState.Value["currentInstanceUrl"] == instanceUrl)
            {
                AppState.Commit(new JObject
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
                HttpUtility.UrlEncode(Resource.GetClientIdByLoginUrl(loginUrl));
            Resource.OpenIncognitoBrowser(oauthPage, AppSettings.GetString(AppConstants.PATH_CHROME));
            return null;
        }
    }
}