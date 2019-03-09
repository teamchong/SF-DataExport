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
    public class DownloadExports
    {
        public JToken Dispatch(JToken payload, AppStateManager appState, ResourceManager resource, JsonConfig orgSettings)
        {
            Rx.FromAsync(async () =>
            {
                var exportPath = payload["exportPath"]?.ToString() ?? "";
                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                var userId = payload["userId"]?.ToString() ?? "";

                var id = orgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                var accessToken = orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                var redirectUri = resource.GetRedirectUrlByLoginUrl(id);
                var targetPage = "/ui/setup/export/DataExportPage/d";
                var targetUrl = string.IsNullOrEmpty(userId) ?
                    instanceUrl + targetPage :
                    resource.GetLoginUrlAs(instanceUrl, id, userId, targetPage);
                var exportResult = new System.Text.StringBuilder();
                exportResult.Append("Loading page ").AppendLine(targetPage);
                var exportResultFiles = new JObject();
                appState.Commit(new JObject { ["exportResult"] = exportResult.ToString(), ["exportResultFiles"] = exportResultFiles });

                try
                {
                    var cookieContainer = new CookieContainer();
                    using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                    {
                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromHours(2);
                            var url = resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
                            var htmlContent = await client.GetStringAsync(url).Continue();
                            htmlContent = await resource.WaitForRedirectAsync(client, instanceUrl, htmlContent, targetUrl).Continue();

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
                                await appState.PageLocationReplaceAsync(redirectUri).Continue();

                                client.DefaultRequestHeaders.Add("Referer", instanceUrl + targetPage);

                                exportResult.Append("Found ").Append(subject.Count).AppendLine(" files.");
                                appState.Commit(new JObject { ["exportResult"] = exportResult.ToString() });

                                if (subject.Count > 0)
                                {
                                    var totalSize = await subject.ToObservable().Select(link => Rx.Defer(() =>
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
                                                    exportResultFiles[fileUrl] = "Downloaded " + resource.GetDisplaySize(fileSize);
                                                    appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                    return Rx.Return(fileSize);
                                                }
                                            }
                                        }
                                        catch { }
                                        return Rx.FromAsync(async () =>
                                        {
                                            exportResultFiles[fileUrl] = "Downloading...";
                                            appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });

                                            using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead).Continue())
                                            {
                                                using (var inStream = await response.Content.ReadAsStreamAsync())
                                                {
                                                    using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                                                    {
                                                        await inStream.CopyToAsync(outStream).Continue();
                                                    }
                                                }
                                            }

                                            using (var zip = ZipFile.OpenRead(outFile))
                                            {
                                                var fileSize = new FileInfo(filename).Length;
                                                exportResultFiles[fileUrl] = "Downloaded " + resource.GetDisplaySize(fileSize);
                                                appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                return fileSize;
                                            }
                                        })
                                        .Catch((Exception ex) => Rx.Defer(() =>
                                        {
                                            exportResultFiles[fileUrl] = "Trying.... " + ex.ToString();
                                            appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                            return Rx.Throw<long>(ex);
                                        }))
                                        .Retry(3)
                                        .Catch((Exception ex) => Rx.Defer(() =>
                                        {
                                            exportResultFiles[fileUrl] = "Failed (" + (++tryCount) + ") " + ex.ToString();
                                            appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                            return Rx.Return(0L);
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
                    appState.PageAlert(ex.Message);
                }
                finally
                {
                    appState.Commit(new JObject { ["isLoading"] = false });
                }
            })
            .SubscribeTask();
            return (JToken)null;
        }
    }
}