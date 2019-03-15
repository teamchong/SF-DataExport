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
    public class GetLimits
    {
        public void Dispatch(AppStateManager appState, JsonConfig orgSettings)
        {
            Observable.FromAsync(async () =>
            {
                appState.Commit(new JObject
                {
                    ["orgLimits"] = new JObject(),
                    ["userLicenses"] = new JArray(),
                });
                var instanceUrl = (string)appState.Value["currentInstanceUrl"];

                if (!string.IsNullOrEmpty(instanceUrl))
                {
                    var accessToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]);
                    var client = new DNFClient(instanceUrl, accessToken);

                    var request = new BatchRequest();
                    request.Limits();
                    request.Query("SELECT Name,UsedLicenses,TotalLicenses FROM UserLicense WHERE Status = 'Active' AND TotalLicenses > 0 ORDER BY Name");
                    var result = await client.Composite.BatchAsync(request);

                    var orgLimits = result.Results("0") ?? new JObject();
                    var userLicenses = new JArray(client.GetEnumerable(result.Queries("1")));
                    appState.Commit(new JObject
                    {
                        ["orgLimits"] = orgLimits,
                        ["userLicenses"] = userLicenses,
                    });
                }
            })
            .ScheduleTask();
        }
    }
}