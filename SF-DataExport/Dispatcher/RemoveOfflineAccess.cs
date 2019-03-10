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
    public class RemoveOfflineAccess
    {
        public void Dispatch(JToken payload, AppStateManager appState, JsonConfig orgSettings)
        {
            Observable.FromAsync(async () =>
            {
                var instanceUrl = (string)payload ?? "";
                await orgSettings.SaveAysnc(json =>
                {
                    if (json[instanceUrl] != null)
                    {
                        json[instanceUrl][OAuth.REFRESH_TOKEN] = "";
                    }
                }).GoOn();
                appState.Commit(new JObject
                {
                    ["orgOfflineAccess"] = new JArray(orgSettings.List()
                    .Where(org => !string.IsNullOrEmpty((string)orgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]))))
                });
            }).ScheduleTask();
        }
    }
}