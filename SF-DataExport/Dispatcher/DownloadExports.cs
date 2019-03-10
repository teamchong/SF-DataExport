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
    public class DownloadExports
    {
        public void Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig orgSettings)
        {
            appState.Commit(new JObject { ["isLoading"] = true });
            Observable.FromAsync(async () =>
            {
                var exportPath = (string)payload["exportPath"] ?? "";
                var instanceUrl = (string)payload["instanceUrl"] ?? "";
                var userId = (string)payload["userId"] ?? "";

                var id = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
                var accessToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
                var redirectUri = resource.GetRedirectUrlByLoginUrl(id);
                var targetPage = "/ui/setup/export/DataExportPage/d";
                var targetUrl = string.IsNullOrEmpty(userId) ?
                    instanceUrl + targetPage :
                    resource.GetLoginUrlAs(instanceUrl, id, userId, targetPage);
                var exportResult = new System.Text.StringBuilder();
                exportResult.Append("Loading page ").AppendLine(targetPage);
                var exportResultFiles = new JObject();
                appState.Commit(new JObject
                {
                    ["exportCount"] = null,
                    ["exportResult"] = exportResult.ToString(),
                    ["exportResultFiles"] = exportResultFiles,
                    ["isLoading"] = false,
                });

                try
                {
                    var cookieContainer = new CookieContainer();
                    using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                    {
                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromHours(2);
                            var url = resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
                            var htmlContent = await client.GetStringAsync(url).GoOn();
                            htmlContent = await resource.WaitForRedirectAsync(client, instanceUrl, htmlContent, targetUrl).GoOn();

                            var doc = new HtmlDocument();
                            doc.LoadHtml(htmlContent);

                            var subject = new List<(string fileName, string fileUrl)>();
                            var links = doc.DocumentNode.SelectNodes(@"//a[contains(@href,"".ZIP"")]");

                            if (links?.Count > 0)
                            {
                                var validHref = new Regex(@"/servlet/servlet.OrgExport\?fileName=(.+\.ZIP)", RegexOptions.IgnoreCase);

                                foreach (var link in links)
                                {
                                    var href = HttpUtility.HtmlDecode(link.GetAttributeValue("href", ""));
                                    var match = validHref.Match(href);
                                    var filename = match?.Groups[1]?.Value;
                                    if (!string.IsNullOrEmpty(filename))
                                    {
                                        if (href.StartsWith('/')) href = instanceUrl + href;
                                        subject.Add((HttpUtility.UrlDecode(filename), href));
                                        exportResultFiles[href] = "Pending...";
                                        appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                    }
                                }

                                Console.WriteLine(subject.Count + " files found for download.");

                                client.DefaultRequestHeaders.Add("Referer", instanceUrl + targetPage);

                                exportResult.Append("Found ").Append(subject.Count).AppendLine(" files.");
                                appState.Commit(new JObject { ["exportCount"] = subject.Count, ["exportResult"] = exportResult.ToString() });

                                if (subject.Count > 0)
                                {
                                    var totalSize = await subject.ToObservable().Select(link => Observable.Defer(() =>
                                    {
                                        var (filename, fileUrl) = link;
                                        var outFile = Path.Combine(exportPath, filename);
                                        var startTime = DateTime.Now;
                                        var tryCount = 0;

                                        try
                                        {
                                            if (File.Exists(outFile))
                                            {
                                                using (var zip = ZipFile.OpenRead(outFile))
                                                {
                                                    var fileSize = new FileInfo(filename).Length;
                                                    exportResultFiles[fileUrl] = "Skipped..." + resource.GetDisplaySize(fileSize);
                                                    appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                    return Observable.Return(fileSize);
                                                }
                                            }
                                        }
                                        catch { }
                                        return Observable.FromAsync(async () =>
                                        {
                                            exportResultFiles[fileUrl] = "Downloading...";
                                            appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });

                                            using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead).GoOn())
                                            {
                                                using (var inStream = await response.Content.ReadAsStreamAsync().GoOn())
                                                {
                                                    using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                                                    {
                                                        await inStream.CopyToAsync(outStream).GoOn();
                                                    }
                                                }
                                            }

                                            using (var zip = ZipFile.OpenRead(outFile))
                                            {
                                                var fileSize = new FileInfo(filename).Length;
                                                exportResultFiles[fileUrl] = "Downloaded..." + resource.GetDisplaySize(fileSize);
                                                appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                return fileSize;
                                            }
                                        })
                                        .Catch((Exception ex) => Observable.Defer(() =>
                                        {
                                            exportResultFiles[fileUrl] = "Trying...." + ex.ToString();
                                            appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                            return Observable.Throw<long>(ex);
                                        }))
                                        .Retry(3)
                                        .Catch((Exception ex) => Observable.Defer(() =>
                                        {
                                            exportResultFiles[fileUrl] = "Failed...(" + (++tryCount) + ") " + ex.ToString();
                                            appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                            return Observable.Return(0L);
                                        }));
                                    }))
                                    .Merge(10)
                                    .Sum();

                                    exportResult.Append("Export completed " + resource.GetDisplaySize(totalSize));
                                    appState.Commit(new JObject { ["exportResult"] = exportResult.ToString(), ["exportResultFiles"] = exportResultFiles });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    appState.Commit(new JObject { ["alertMessage"] = ex.Message });
                }
                finally
                {
                    appState.Commit(new JObject { ["isLoading"] = false });
                }
            }).ScheduleTask();
        }
    }
}