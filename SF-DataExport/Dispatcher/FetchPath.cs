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
    public class FetchPath
    {
        public void Dispatch(JToken payload, AppStateManager appState)
        {
            var search = ((string)payload?["search"])?.Trim() ?? "";
            var value = ((string)payload?["value"]) ?? "";
            var field = ((string)payload?["field"]) ?? "";

            if (search != "" && field != "")
            {
                try
                {
                    if (Directory.Exists(search))
                    {
                        appState.Commit(new JObject
                        {
                            [field] = new JArray(new[] { value }.Where(s => s != "")
                                .Concat(Directory.GetFiles(search)).Distinct())
                        });
                        return;
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(search);
                        if (Directory.Exists(dir))
                        {
                            if (File.Exists(search))
                            {
                                appState.Commit(new JObject
                                {
                                    [field] = new JArray(new[] { search, value }.Where(s => s != "")
                                    .Concat(Directory.GetFiles(dir)).Distinct())
                                });
                                return;
                            }
                            else
                            {
                                appState.Commit(new JObject
                                {
                                    [field] = new JArray(new[] { value }.Where(s => s != "")
                                    .Concat(Directory.GetDirectories(dir)).Distinct())
                                });
                                return;
                            }
                        }
                    }
                }
                catch { }
            }
            appState.Commit(new JObject { [field] = new JArray(new[] { value }.Where(s => s != "")) });
        }
    }
}