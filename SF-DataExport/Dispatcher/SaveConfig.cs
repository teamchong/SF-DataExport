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
    public class SaveConfig
    {
        public JToken Dispatch(JToken payload, AppStateManager appState, JsonConfig appSettings, JsonConfig orgSettings)
        {
            Rx.Start(() =>
            {
                var config = payload as JObject;
                var chromePath = config?[AppConstants.CHROME_PATH]?.ToString();
                var orgSettingsPath = config?[AppConstants.ORG_SETTINGS_PATH]?.ToString();
                var newchromePath = config?[AppConstants.CHROME_PATH]?.ToString();

                Rx.Merge(
                    Rx.FromAsync(() => appState.SaveOrgSettingsPathAsync(orgSettingsPath))
                        .Catch((Exception ex) => Rx.Return(ex.ToString())),
                        Rx.FromAsync(() => appSettings.SaveAysnc(o => o[AppConstants.CHROME_PATH] = chromePath))
                        .Select(_ => (string)null)
                        .Catch((Exception ex) => Rx.Return(ex.ToString()))
                    )
                    .Scan(new List<string>(), (errorMessages, errorMessage) =>
                    {
                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            errorMessages.Add(errorMessage);
                        }
                        return errorMessages;
                    })
                    .Select(errorMessages => Rx.Start(() =>
                    {
                        var message = string.Join(Environment.NewLine, errorMessages);
                        if (errorMessages.Count <= 0)
                        {
                            appState.PageAlert("Save successfully.");
                        }
                        else
                        {
                            appState.PageAlert(message);
                        }
                    }))
                    .SubscribeTask();
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}