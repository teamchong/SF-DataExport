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
    public class RemoveOfflineAccess
    {
        public JToken Dispatch(JToken payload, AppStateManager appState, JsonConfig orgSettings)
        {
            Rx.FromAsync(async () =>
            {
                var instanceUrl = payload?.ToString() ?? "";
                await orgSettings.SaveAysnc(json =>
                {
                    if (json[instanceUrl] != null)
                    {
                        json[instanceUrl][OAuth.REFRESH_TOKEN] = "";
                    }
                }).Continue();
                appState.Commit(new JObject
                {
                    ["orgOfflineAccess"] = new JArray(orgSettings.List()
                    .Where(org => !string.IsNullOrEmpty(orgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN])?.ToString())))
                });
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}