using DotNetForce;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Web;

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
                var exportEmails = (string)payload["exportEmails"] ?? "";

                var id = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ID]) ?? "";
                var accessToken = (string)orgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]) ?? "";
                var redirectUri = resource.GetRedirectUrlByLoginUrl(id);
                var targetPage = "/ui/setup/export/DataExportPage/d";
                var targetUrl = instanceUrl + targetPage;
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
                    await resource.RunClientAsync(async (httpClient, cookieContainer, htmlContent) =>
                    {
                        //httpClient.Timeout = TimeSpan.FromHours(2);

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
                        }

                        Console.WriteLine(subject.Count + " files found for download.");
                        exportResult.Append("Found ").Append(subject.Count).AppendLine(" files.");
                        appState.Commit(new JObject { ["exportCount"] = subject.Count, ["exportResult"] = exportResult.ToString() });

                        if (subject.Count > 0)
                        {
                            httpClient.DefaultRequestHeaders.Add("Referer", instanceUrl + targetPage);
                            var totalSize = await subject.ToObservable(TaskPoolScheduler.Default).Select(link =>
                                    {
                                        var (fileName, fileUrl) = link;
                                        return Observable.Defer(() =>
                                        {
                                            var outFile = Path.Combine(exportPath, fileName);

                                            if (File.Exists(outFile))
                                            {
                                                Console.WriteLine("File exists, validating... " + fileName);
                                                using (var zip = ZipFile.OpenRead(outFile))
                                                {
                                                    var entities = zip.Entries;
                                                    if (entities?.Count > 0)
                                                    {
                                                        var fileSize = new FileInfo(fileName).Length;
                                                        Console.WriteLine("Skipped... " + fileName);
                                                        exportResultFiles[fileUrl] = "Skipped..." + resource.GetDisplaySize(fileSize);
                                                        appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                        return Observable.Return(fileSize);
                                                    }
                                                }
                                            }
                                            return Observable.Throw<long>(new FileNotFoundException());
                                        })
                                        .Catch((Exception _ex) =>
                                        {
                                            var tryCount = 0;
                                            return Observable.FromAsync(async () =>
                                            {
                                                var outFile = Path.Combine(exportPath, fileName);
                                                if (File.Exists(outFile))
                                                {
                                                    File.Delete(outFile);
                                                    Console.WriteLine("Deleted invalid file " + fileName);
                                                }
                                                Console.WriteLine("Downloading..." + fileName);
                                                exportResultFiles[fileUrl] = "Downloading...";
                                                appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });

                                                using (var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead).GoOn())
                                                {
                                                    using (var inStream = await response.Content.ReadAsStreamAsync())
                                                    {
                                                        using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                                                        {
                                                            await inStream.CopyToAsync(outStream, 1024 * 4);
                                                        }
                                                    }
                                                }

                                                using (var zip = ZipFile.OpenRead(outFile))
                                                {
                                                    var fileSize = new FileInfo(fileName).Length;
                                                    Console.WriteLine("Downloaded... " + fileName);
                                                    exportResultFiles[fileUrl] = "Downloaded..." + resource.GetDisplaySize(fileSize);
                                                    appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                    return fileSize;
                                                }
                                            })
                                            .Catch((Exception ex) => Observable.Defer(() =>
                                            {
                                                Console.WriteLine("Trying... " + fileName + "\n" + ex.ToString());
                                                exportResultFiles[fileUrl] = "Trying..." + ex.ToString();
                                                appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                return Observable.Throw<long>(ex);
                                            }))
                                            .Retry(10)
                                            .Catch((Exception ex) => Observable.Defer(() =>
                                            {
                                                Console.WriteLine("Failed... " + fileName + "\n" + ex.ToString());
                                                exportResultFiles[fileUrl] = "Failed...(" + (++tryCount) + ") " + ex.ToString();
                                                appState.Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                return Observable.Return(0L);
                                            }));
                                        });
                                    })
                                    .Merge(10)
                                    .Sum();

                            var downloaded = 0;
                            var failed = 0;

                            foreach (var fileUrl in exportResultFiles.Properties().Select(p => p.Name).ToList())
                            {
                                var result = (string)exportResultFiles[fileUrl];
                                if (result?.StartsWith("Downloaded...") == true)
                                    downloaded++;
                                else if (result?.StartsWith("Failed...") == true)
                                    failed++;
                            }
                            if ((downloaded > 0 || failed > 0) && !string.IsNullOrEmpty(exportEmails))
                            {
                                var client = new DNFClient(instanceUrl, accessToken);
                                await Observable.FromAsync(() => client.JsonHttp.HttpPostAsync<JArray>(new JObject
                                {
                                    ["inputs"] = new JArray(
                                        new JObject
                                        {
                                            ["emailBody"] = "NT",
                                            ["emailAddresses"] = exportEmails,
                                            ["emailSubject"] = "Download Data Exports" +
                                                (downloaded > 0 ? " " + downloaded + " downloaded" : "") +
                                                (failed > 0 ? " " + failed + " failed" : "") +
                                                " total " + resource.GetDisplaySize(totalSize),
                                            ["senderType"] = "CurrentUser",
                                        })
                                }, new Uri($"{client.InstanceUrl}/services/data/{client.ApiVersion}/actions/standard/emailSimple")))
                                .SelectMany(result => Observable.Defer(() => Observable.Start(() => Console.WriteLine(result?.ToString()))))
                                .Catch((Exception ex) => Observable.Defer(() => Observable.Start(() => Console.WriteLine(ex.ToString()))))
                                .SubscribeOn(TaskPoolScheduler.Default);
                            }
                            Console.WriteLine("Export completed " + resource.GetDisplaySize(totalSize));
                            exportResult.Append("Export completed " + resource.GetDisplaySize(totalSize));
                            appState.Commit(new JObject
                            {
                                ["exportResult"] = exportResult.ToString(),
                                ["exportResultFiles"] = exportResultFiles
                            });

                        }
                        return 0;
                    }, instanceUrl, accessToken, targetUrl).GoOn();
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