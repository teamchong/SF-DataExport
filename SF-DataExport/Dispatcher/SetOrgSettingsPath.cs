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
    public class SetOrgSettingsPath
    {
        public void Dispatch(JToken payload, AppStateManager appState)
        {
            appState.Commit(new JObject { ["isLoading"] = true });
            Observable.FromAsync(async () =>
            {
                var orgSettingsPath = (string)payload ?? "";
                var errorMessage = await appState.SaveOrgSettingsPathAsync(orgSettingsPath).GoOn();
                if (errorMessage == null)
                {
                    appState.Commit(new JObject { ["alertMessage"] = "Save successfully." });
                }
                else
                {
                    appState.Commit(new JObject { ["alertMessage"] = "No change." });
                }
            })
            .Finally(() => appState.Commit(new JObject { ["isLoading"] = false }))
            .ScheduleTask();
        }
    }
}