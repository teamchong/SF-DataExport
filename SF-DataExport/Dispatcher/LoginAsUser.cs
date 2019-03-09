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
    public class LoginAsUser
    {
        public JToken Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings)
        {
            Rx.Start(() =>
            {
                var instanceUrl = payload?["instanceUrl"]?.ToString() ?? "";
                var userId = payload?["userId"]?.ToString() ?? "";
                var accessToken = orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                var id = orgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                var targetUrl = resource.GetLoginUrlAs(instanceUrl, id, userId, "/");
                var urlWithAccessCode = resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
                resource.OpenBrowserIncognito(urlWithAccessCode, appSettings.GetString(AppConstants.CHROME_PATH));
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}