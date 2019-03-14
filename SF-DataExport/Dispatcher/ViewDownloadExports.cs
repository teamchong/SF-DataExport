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
    public class ViewDownloadExports
    {
        public void Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings)
        {
            var exportPath = (string)payload?["exportPath"] ?? "";
            var instanceUrl = (string)payload?["instanceUrl"] ?? "";
            var userId = (string)payload?["userId"] ?? "";

            var id = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
            var accessToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
            var redirectUri = resource.GetRedirectUrlByLoginUrl(id);
            var targeturl = string.IsNullOrEmpty(userId) ?
                instanceUrl + "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport" :
                resource.GetLoginUrlAs(instanceUrl, id, userId,
                "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport");
            var urlWithAccessCode = resource.GetUrlViaAccessToken(instanceUrl, accessToken, targeturl);
            resource.OpenIncognitoBrowser(urlWithAccessCode, appSettings.GetString(AppConstants.PATH_CHROME));
        }
    }
}