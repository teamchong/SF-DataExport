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
    public class FetchPath
    {
        public JToken Dispatch(JToken payload, AppStateManager appState)
        {
            Rx.Start(() =>
            {
                var search = payload?["search"]?.ToString()?.Trim() ?? "";
                var value = payload?["value"]?.ToString() ?? "";
                var field = payload?["field"]?.ToString() ?? "";

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
                                }
                                else
                                {
                                    appState.Commit(new JObject
                                    {
                                        [field] = new JArray(new[] { value }.Where(s => s != "")
                                        .Concat(Directory.GetDirectories(dir)).Distinct())
                                    });
                                }
                                return;
                            }
                        }
                    }
                    catch { }
                }
                appState.Commit(new JObject { [field] = new JArray(new[] { value }.Where(s => s != "")) });
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}