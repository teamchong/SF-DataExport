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
    public class SetOrgSettingsPath : IDispatcher
    {
        AppStateManager AppState { get; }

        public SetOrgSettingsPath(AppStateManager appState)
        {
            AppState = appState;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            try
            {
                AppState.Commit(new JObject { ["isLoading"] = true });

                var orgSettingsPath = (string)payload ?? "";
                var errorMessage = await AppState.SaveOrgSettingsPathAsync(orgSettingsPath).GoOn();
                if (errorMessage == null)
                {
                    AppState.Commit(new JObject { ["alertMessage"] = "Save successfully." });
                }
                else
                {
                    AppState.Commit(new JObject { ["alertMessage"] = "No change." });
                }
            }
            finally
            {
                AppState.Commit(new JObject { ["isLoading"] = false });
            }
            return null;
        }
    }
}