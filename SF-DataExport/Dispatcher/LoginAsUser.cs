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
    public class LoginAsUser
    {
        public void Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings)
        {
            var instanceUrl = (string)payload?["instanceUrl"] ?? "";
            var id = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
            var userId = (string)payload?["userId"] ?? "";
            if (userId == "") userId = id.Split('/').LastOrDefault();
            var page = (string)payload?["page"] ?? "";
            if (page == "") page = "/";
            var accessToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
            var targetUrl = resource.GetLoginUrlAs(instanceUrl, id, userId, page);
            var urlWithAccessCode = resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
            resource.OpenIncognitoBrowser(urlWithAccessCode, appSettings.GetString(AppConstants.PATH_CHROME));
        }
    }
}