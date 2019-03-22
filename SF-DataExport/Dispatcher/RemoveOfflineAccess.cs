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
    public class RemoveOfflineAccess : IDispatcher
    {
        AppStateManager AppState { get; }
        JsonConfig OrgSettings { get; }

        public RemoveOfflineAccess(AppStateManager appState, JsonConfig orgSettings)
        {
            AppState = AppState;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)payload ?? "";
            await OrgSettings.SaveAysnc(json =>
            {
                if (json[instanceUrl] != null)
                {
                    json[instanceUrl][OAuth.REFRESH_TOKEN] = "";
                }
            }).GoOn();
            AppState.Commit(new JObject
            {
                ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                .Where(org => !string.IsNullOrEmpty((string)OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]))))
            });
            return null;
        }
    }
}