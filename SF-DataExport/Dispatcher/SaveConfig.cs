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
    public class SaveConfig
    {
        public void Dispatch(JToken payload, AppStateManager appState, JsonConfig appSettings, JsonConfig orgSettings)
        {
            appState.Commit(new JObject { ["isLoading"] = true });
            var config = payload as JObject;
            var chromePath = (string)config?[AppConstants.PATH_CHROME];
            var orgSettingsPath = (string)config?[AppConstants.PATH_ORG_SETTINGS];
            var newchromePath = (string)config?[AppConstants.PATH_CHROME];

            Observable.Merge(
                Observable.FromAsync(() => appState.SaveOrgSettingsPathAsync(orgSettingsPath))
                .Catch((Exception ex) => Observable.Return(ex.ToString())),

                Observable.FromAsync(() => appSettings.SaveAysnc(o => o[AppConstants.PATH_CHROME] = chromePath)).Select(_ => (string)null)
                .Catch((Exception ex) => Observable.Return(ex.ToString()))
            )
            .Scan(new List<string>(), (errorMessages, errorMessage) =>
            {
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    errorMessages.Add(errorMessage);
                }
                return errorMessages;
            })
            .Select(errorMessages => Observable.Defer(() =>
            {
                var message = string.Join(Environment.NewLine, errorMessages);
                if (errorMessages.Count <= 0)
                {
                    appState.Commit(new JObject { ["alertMessage"] = "Save successfully.", ["isLoading"] = false });
                }
                else
                {
                    appState.Commit(new JObject { ["alertMessage"] = message, ["isLoading"] = false });
                }
                return Observable.Empty<Unit>();
            }))
            .ScheduleTask();
        }
    }
}