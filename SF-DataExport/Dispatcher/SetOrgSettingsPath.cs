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
    public class SetOrgSettingsPath
    {
        public JToken Dispatch(JToken payload, AppStateManager appState)
        {
            Rx.FromAsync(async () =>
            {
                var orgSettingsPath = payload?.ToString() ?? "";
                var errorMessage = await appState.SaveOrgSettingsPathAsync(orgSettingsPath).Continue();
                if (errorMessage == null)
                {
                    appState.PageAlert("Save successfully.");
                }
                else
                {
                    appState.PageAlert("No change.");
                }
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}