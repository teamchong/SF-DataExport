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
    public class ViewPage
    {
        public void Dispatch(string targetUrl, AppStateManager appState, ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings)
        {
            var instanceUrl = (string)appState.Value["currentInstanceUrl"] ?? "";
            var accessToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
            var urlWithAccessCode = resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
            resource.OpenIncognitoBrowser(urlWithAccessCode, appSettings.GetString(AppConstants.PATH_CHROME));
        }
    }
}