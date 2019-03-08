using DotNetForce;
using HtmlAgilityPack;
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
using System.Threading.Tasks;
using System.Web;
using Rx = System.Reactive.Linq.Observable;
using Unit = System.Reactive.Unit;

namespace SF_DataExport
{
    public class AppStateManager
    {
        public JsonMergeSettings MergeSettings = new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge,
            PropertyNameComparison = StringComparison.CurrentCulture,
        };

        ResourceManager Resource { get; set; }
        Page AppPage { get; set; }
        JsonConfig AppSettings { get; set; }
        JsonConfig OrgSettings { get; set; }
        JObject State { get; set; }
        Subject<JObject> CommitSubject = new Subject<JObject>();

        public JObject Value { get => State; }

        public AppStateManager(Page appPage, JsonConfig appSettings, JsonConfig orgSettings, ResourceManager resource)
        {
            AppPage = appPage;
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            State = new JObject
            {
                ["chromePath"] = AppSettings.GetString(AppConstants.CHROME_PATH),
                ["chromePathItems"] = new JArray(AppSettings.GetString(AppConstants.CHROME_PATH)),
                ["cmdExport"] = "dotnet " + AppDomain.CurrentDomain.FriendlyName + ".dll download@",
                ["currentInstanceUrl"] = "",
                ["exportPath"] = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                ["exportPathItems"] = new JArray(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)),
                ["exportResult"] = "",
                ["exportResultFiles"] = new JArray(),
                ["exportUserId"] = "",
                ["isLoading"] = false,
                ["showOrgModal"] = true,
                ["showUserPopover"] = true,
                ["orgSettingsPath"] = OrgSettings.GetDirectoryPath(),
                ["orgSettingsPathItems"] = new JArray(OrgSettings.GetDirectoryPath()),
                ["popoverUserId"] = "",
                ["tab"] = "downloaddataexport", //"setup"
                ["userDisplayName"] = "",
                ["userEmail"] = "",
                ["userId"] = "",
                ["userName"] = "",
                ["userPhoto"] = "",
                ["users"] = new JArray(),
            };
            State.Merge(GetOrgSettings(), MergeSettings);
            Subscribe();
        }

        public async Task<Response> PageRedirectAsync(string url)
        {
            await AppPage.EvaluateExpressionAsync("location.href='" + HttpUtility.JavaScriptStringEncode(url) + "'").Continue();
            return await AppPage.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).Continue();
        }

        public async Task<Response> PageLocationReplaceAsync(string url)
        {
            await AppPage.EvaluateExpressionAsync("location.replace('" + HttpUtility.JavaScriptStringEncode(url) + "')").Continue();
            return await AppPage.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).Continue();
        }

        public void PageAlert(string message)
        {
            Rx.FromAsync(() => AppPage.EvaluateFunctionAsync("alert", message ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeTask();
        }

        public void PageConfirm(string message)
        {
            Rx.FromAsync(() => AppPage.EvaluateFunctionAsync("confirm", message ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeTask();
        }

        public void PagePrompt(string message)
        {
            PagePrompt(message, "");
        }

        public void PagePrompt(string message, string defaultValue)
        {
            Rx.FromAsync(() => AppPage.EvaluateFunctionAsync("prompt", message ?? "", defaultValue ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeTask();
        }

        public JToken SubscribeDispatch(JArray actions)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i] as JObject;
                if (action != null)
                {
                    var type = action["type"]?.ToString();
                    var payload = action["payload"] as JObject;

                    switch (type)
                    {
                        case "fetchDirPath":
                            Rx.Start(() =>
                            {
                                var search = payload["search"]?.ToString()?.Trim()?.TrimEnd(Path.DirectorySeparatorChar) ?? "";
                                var value = payload["value"]?.ToString() ?? "";
                                var field = payload["field"]?.ToString() ?? "";

                                if (search != "" && field != "")
                                {
                                    try
                                    {
                                        if (Directory.Exists(search))
                                        {
                                            Commit(new JObject
                                            {
                                                [field] = new JArray(new[] { search.TrimEnd(Path.DirectorySeparatorChar), value }.Where(s => s != "")
                                                .Concat(Directory.GetDirectories(search)).Select(d => d.TrimEnd(Path.DirectorySeparatorChar)).Distinct())
                                            });
                                            return;
                                        }
                                        else
                                        {
                                            var dir = Path.GetDirectoryName(search);
                                            if (Directory.Exists(dir))
                                            {
                                                Commit(new JObject
                                                {
                                                    [field] = new JArray(new[] { dir.TrimEnd(Path.DirectorySeparatorChar), value }.Where(s => s != "")
                                                    .Concat(Directory.GetDirectories(dir)).Select(d => d.TrimEnd(Path.DirectorySeparatorChar)).Distinct())
                                                });
                                                return;
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                Commit(new JObject { [field] = new JArray(new[] { value }.Where(s => s != "")) });
                            })
                            .SubscribeTask();
                            break;
                        case "fetchPath":
                            Rx.Start(() =>
                            {
                                var search = payload["search"]?.ToString()?.Trim() ?? "";
                                var value = payload["value"]?.ToString() ?? "";
                                var field = payload["field"]?.ToString() ?? "";

                                if (search != "" && field != "")
                                {
                                    try
                                    {
                                        if (Directory.Exists(search))
                                        {
                                            Commit(new JObject
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
                                                    Commit(new JObject
                                                    {
                                                        [field] = new JArray(new[] { search, value }.Where(s => s != "")
                                                        .Concat(Directory.GetFiles(dir)).Distinct())
                                                    });
                                                }
                                                else
                                                {
                                                    Commit(new JObject
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
                                Commit(new JObject { [field] = new JArray(new[] { value }.Where(s => s != "")) });
                            })
                            .SubscribeTask();
                            break;
                        case "loginAsUser":
                            Rx.Start(() =>
                            {
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var id = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                                var targetUrl = Resource.GetLoginUrlAs(instanceUrl, id, userId, "/");
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.GetString(AppConstants.CHROME_PATH));
                            })
                            .SubscribeTask();
                            break;
                        case "viewPage":
                            Rx.Start(() =>
                            {
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var url = payload["url"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, url);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.GetString(AppConstants.CHROME_PATH));
                            })
                            .SubscribeTask();
                            break;
                        case "viewUserPage":
                            Rx.Start(() =>
                            {
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var targetUrl = instanceUrl + "/" + userId + "?noredirect=1";
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.GetString(AppConstants.CHROME_PATH));
                            })
                            .SubscribeTask();
                            break;
                        case "viewDownloadExports":
                            Rx.Start(() =>
                            {
                                var exportPath = payload["exportPath"]?.ToString() ?? "";
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";

                                var id = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var redirectUri = Resource.GetRedirectUrlByLoginUrl(id);
                                var targeturl = string.IsNullOrEmpty(userId) ?
                                    instanceUrl + "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport" :
                                    Resource.GetLoginUrlAs(instanceUrl, id, userId,
                                    "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport");
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, targeturl);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.GetString(AppConstants.CHROME_PATH));
                            })
                            .SubscribeTask();
                            break;
                        case "downloadExports":
                            Rx.FromAsync(async () =>
                            {
                                var exportPath = payload["exportPath"]?.ToString() ?? "";
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";

                                var id = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var redirectUri = Resource.GetRedirectUrlByLoginUrl(id);
                                var targetPage = "/ui/setup/export/DataExportPage/d";
                                var targetUrl = string.IsNullOrEmpty(userId) ?
                                    instanceUrl + targetPage :
                                    Resource.GetLoginUrlAs(instanceUrl, id, userId, targetPage);
                                var exportResult = new System.Text.StringBuilder();
                                exportResult.Append("Loading page ").AppendLine(targetPage);
                                var exportResultFiles = new JObject();
                                Commit(new JObject { ["exportResult"] = exportResult.ToString(), ["exportResultFiles"] = exportResultFiles });

                                try
                                {
                                    var cookieContainer = new CookieContainer();
                                    using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                                    {
                                        using (var client = new HttpClient(handler))
                                        {
                                            client.Timeout = TimeSpan.FromHours(2);
                                            var url = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
                                            var htmlContent = await client.GetStringAsync(url).Continue();
                                            htmlContent = await Resource.WaitForRedirectAsync(client, instanceUrl, htmlContent, targetUrl).Continue();

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
                                                        Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                    }
                                                }

                                                Console.WriteLine(subject.Count + " files found for download.");
                                                await PageLocationReplaceAsync(redirectUri).Continue();

                                                client.DefaultRequestHeaders.Add("Referer", instanceUrl + targetPage);

                                                exportResult.Append("Found ").Append(subject.Count).AppendLine(" files.");
                                                Commit(new JObject { ["exportResult"] = exportResult.ToString() });

                                                if (subject.Count > 0)
                                                {
                                                    var totalSize = await subject.ToObservable().Select(link =>
                                                    {
                                                        var (filename, fileUrl) = link;
                                                        return Rx.FromAsync(async () =>
                                                        {
                                                            exportResultFiles[fileUrl] = "Downloading...";
                                                            Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                            var outFile = Path.Combine(exportPath, filename);

                                                            using (var inStream = await client.GetStreamAsync(fileUrl).Continue())
                                                            {
                                                                using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                                                                {
                                                                    await inStream.CopyToAsync(outStream).Continue();
                                                                }
                                                            }

                                                            using (var zip = ZipFile.OpenRead(outFile))
                                                            {
                                                                exportResultFiles[fileUrl] = "Done. " + GetDisplaySize(outFile);
                                                                Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                                return outFile;
                                                            }
                                                        })
                                                        .Catch((Exception ex) => Rx.Defer(() =>
                                                        {
                                                            exportResultFiles[fileUrl] = ex.ToString();
                                                            Commit(new JObject { ["exportResultFiles"] = exportResultFiles });
                                                            return Rx.Throw<string>(ex);
                                                        }));
                                                    })
                                                    .Retry(3)
                                                    .Merge(10)
                                                    .Count();

                                                    exportResult.Append("Export completed");
                                                    Commit(new JObject { ["exportResult"] = exportResult.ToString(), ["exportResultFiles"] = exportResultFiles });
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    PageAlert(ex.Message);
                                }
                                finally
                                {
                                    Commit(new JObject { ["isLoading"] = false });
                                }
                            })
                            .SubscribeTask();
                            break;
                        case "removeOrg":
                            Rx.FromAsync(async () =>
                            {
                                var instanceUrl = action["payload"]?.ToString() ?? "";
                                var loginUrl = Resource.GetLoginUrl(OrgSettings.Get(o => o[instanceUrl][OAuth.ID]));
                                await OrgSettings.SaveAysnc(json =>
                                    {
                                        if (json[instanceUrl] != null)
                                        {
                                            json.Remove(instanceUrl);
                                        }
                                    }).Continue();
                                Commit(GetOrgSettings());
                                if (State["currentInstanceUrl"]?.ToString() == instanceUrl)
                                {
                                    Commit(new JObject
                                    {
                                        ["currentInstanceUrl"] = "",
                                        ["exportUserId"] = "",
                                        ["popoverUserId"] = "",
                                        ["showOrgModal"] = true,
                                        ["userDisplayName"] = "",
                                        ["userEmail"] = "",
                                        ["userId"] = "",
                                        ["userName"] = "",
                                        ["userPhoto"] = "",
                                        ["users"] = new JArray()
                                    });
                                }
                                var oauthPage = instanceUrl +
                                    "/identity/app/connectedAppsUserList.apexp?app_name=SFDataExport&consumer_key=" +
                                    HttpUtility.UrlEncode(Resource.GetClientIdByLoginUrl(loginUrl));
                                Resource.OpenBrowserIncognito(oauthPage, AppSettings.GetString(AppConstants.CHROME_PATH));
                            })
                            .SubscribeTask();
                            break;
                        case "removeOfflineAccess":
                            Rx.FromAsync(async () =>
                            {
                                var instanceUrl = action["payload"]?.ToString() ?? "";
                                await OrgSettings.SaveAysnc(json =>
                                {
                                    if (json[instanceUrl] != null)
                                    {
                                        json[instanceUrl][OAuth.REFRESH_TOKEN] = "";
                                    }
                                }).Continue();
                                Commit(new JObject
                                {
                                    ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                                    .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN])?.ToString())))
                                });
                            })
                            .SubscribeTask();
                            break;
                        case "saveConfig":
                            Rx.Start(() =>
                            {
                                var config = action["payload"] as JObject;
                                var chromePath = config?[AppConstants.CHROME_PATH]?.ToString();
                                var orgSettingsPath = config?[AppConstants.ORG_SETTINGS_PATH]?.ToString();
                                var newchromePath = config?[AppConstants.CHROME_PATH]?.ToString();

                                Rx.Merge(
                                    Rx.FromAsync(() => SaveOrgSettingsPathAsync(orgSettingsPath))
                                        .Catch((Exception ex) => Rx.Return(ex.ToString())),
                                        Rx.FromAsync(() => AppSettings.SaveAysnc(o => o[AppConstants.CHROME_PATH] = chromePath))
                                        .Select(_ => (string)null)
                                        .Catch((Exception ex) => Rx.Return(ex.ToString()))
                                    )
                                    .Scan(new List<string>(), (errorMessages, errorMessage) =>
                                    {
                                        if (!string.IsNullOrEmpty(errorMessage))
                                        {
                                            errorMessages.Add(errorMessage);
                                        }
                                        return errorMessages;
                                    })
                                    .Select(errorMessages => Rx.Start(() =>
                                    {
                                        var message = string.Join(Environment.NewLine, errorMessages);
                                        if (errorMessages.Count <= 0)
                                        {
                                            PageAlert("Save successfully.");
                                        }
                                        else
                                        {
                                            PageAlert(message);
                                        }
                                    }))
                                    .SubscribeTask();
                            })
                            .SubscribeTask();
                            break;
                        case "setOrgSettingsPath":
                            Rx.FromAsync(async () =>
                            {
                                var orgSettingsPath = action["payload"]?.ToString() ?? "";
                                var errorMessage = await SaveOrgSettingsPathAsync(orgSettingsPath).Continue();
                                if (errorMessage == null)
                                {
                                    PageAlert("Save successfully.");
                                }
                                else
                                {
                                    PageAlert("No change.");
                                }
                            })
                            .SubscribeTask();
                            break;
                        case "attemptLogin":
                            Rx.FromAsync(async () =>
                            {
                                var attemptingDomain = Regex.Replace(action["payload"]?.ToString() ?? "", "^https?://", "");

                                if (!string.IsNullOrEmpty(attemptingDomain))
                                {
                                    var loginUrl = "https://" + attemptingDomain;

                                    if (attemptingDomain != "login.salesforce.com" && attemptingDomain != "test.salesforce.com")
                                    {
                                        var instanceUrl = "https://" + attemptingDomain;
                                        var savedOrg = OrgSettings.Get(o => o[instanceUrl]);
                                        if (savedOrg != null)
                                        {
                                            var accessToken = savedOrg[OAuth.ACCESS_TOKEN]?.ToString() ?? "";
                                            var refreshToken = savedOrg[OAuth.REFRESH_TOKEN]?.ToString() ?? "";
                                            loginUrl = Resource.GetLoginUrl(savedOrg[OAuth.ID]);
                                            if (!Uri.IsWellFormedUriString(loginUrl, UriKind.Absolute))
                                            {
                                                loginUrl = "https://login.salesforce.com";
                                            }
                                            var client = new DNFClient(instanceUrl, accessToken, refreshToken);

                                            try
                                            {
                                                await client.TokenRefreshAsync(new Uri(loginUrl), Resource.GetClientIdByLoginUrl(loginUrl))
                                                    .Continue();
                                                await SetOrganizationAsync(
                                                    client.AccessToken,
                                                    client.InstanceUrl,
                                                    client.Id,
                                                    client.RefreshToken)
                                                    .Continue();
                                                SetCurrentInstanceUrl(client);
                                                return;
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine(ex.ToString());
                                                PageAlert(ex.Message);
                                            }
                                        }
                                    }

                                    var targetUrl = loginUrl + "/services/oauth2/authorize" +
                                        "?response_type=token" +
                                        "&client_id=" + HttpUtility.UrlEncode(Resource.GetClientIdByLoginUrl(loginUrl)) +
                                        "&redirect_uri=" + HttpUtility.UrlEncode(Resource.GetRedirectUrlByLoginUrl(loginUrl)) +
                                        "&state=" + HttpUtility.UrlEncode(loginUrl) +
                                        "&display=popup";
                                    await PageRedirectAsync(targetUrl).Continue();
                                }
                            })
                            .SubscribeTask();
                            break;
                        default:
                            Commit(new JObject { [type] = action["payload"] });
                            break;
                    }
                }
            }
            return (JToken)null;
        }

        public string GetDisplaySize(string filename)
        {
            var sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
            var len = new FileInfo(filename).Length;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            var result = String.Format("{0:0.##} {1}", len, sizes[order]);
            return result;
        }

        public void Subscribe()
        {
            CommitSubject.Buffer(TimeSpan.FromMilliseconds(100)).Where(newStates => newStates.Any())
                .Select(newStates =>
                    Rx.FromAsync(() =>
                    {
                        if (Resource.IsRedirectPage(AppPage.Url))
                        {
                            var expression = new System.Text.StringBuilder("if(typeof storeCommit!=='undefined'){try{storeCommit([");
                            expression.Append(string.Join(",", newStates.Select(newState => newState.ToString(0))));
                            expression.Append("])}catch(_){}}");
                            return AppPage.EvaluateExpressionAsync(expression.ToString());
                        }
                        return Task.FromResult<JToken>(null);
                    })
                    .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                 )
                .Concat()
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe();
        }

        public JObject GetOrgSettings()
        {
            var orgList = OrgSettings.List();
            var orgOfflineAccess = new JArray(orgList
                    .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN])?.ToString())));
            var orgSandboxes = new JArray(orgList
                    .Where(org => Resource.IsSandboxLoginUrl(Resource.GetLoginUrl(OrgSettings.Get(o => o[org]?[OAuth.ID])))));
            return new JObject
            {
                ["orgOfflineAccess"] = orgOfflineAccess,
                ["orgSandboxes"] = orgSandboxes,
                ["orgSettings"] = new JArray(orgList),
            };
        }

        public void Commit(JObject newState)
        {
            if (newState != null)
            {
                try
                {
                    State.Merge(newState, MergeSettings);
                    CommitSubject.OnNext(newState);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public void SetCurrentInstanceUrl(DNFClient client)
        {
            Commit(new JObject
            {
                ["currentInstanceUrl"] = client.InstanceUrl,
                ["exportUserId"] = "",
                ["popoverUserId"] = "",
                ["showOrgModal"] = false,
                ["userDisplayName"] = "",
                ["userEmail"] = "",
                ["userId"] = "",
                ["userName"] = "",
                ["userPhoto"] = "",
                ["users"] = new JArray()
            });
            Rx.Merge(

                Rx.FromAsync(() => client.GetEnumerableAsync("SELECT Id,Name,Email FROM User WHERE IsActive=true ORDER BY Name,Email"))
                .Select(records => new JArray(records))
                .SelectMany(users => Rx.If(
                    () => State["currentInstanceUrl"]?.ToString() == client.InstanceUrl,
                    Rx.Start(() => Commit(new JObject { ["users"] = users })),
                    Rx.Throw<Unit>(new InvalidOperationException())
                ))
                .Catch(Rx.Empty<Unit>()),

                Rx.FromAsync(() => client.UserInfo())
                .SelectMany(userInfo => Rx.Merge(

                    Rx.Start(() => Commit(new JObject
                    {
                        ["userDisplayName"] = userInfo?["display_name"],
                        ["userEmail"] = userInfo?["email"],
                        ["userId"] = client.Id.Split('/').Last(),
                        ["userName"] = userInfo?["username"],
                    }))
                    .Catch(Rx.Empty<Unit>()),

                    Rx.FromAsync(() => Resource.GetDataUriViaAccessToken(client.InstanceUrl, client.AccessToken,
                        userInfo?["photos"]?["thumbnail"]?.ToString(), "image/png"))
                    .SelectMany(userPhoto =>
                        Rx.If(
                            () => State["currentInstanceUrl"]?.ToString() == client.InstanceUrl,
                            Rx.Start(() => Commit(new JObject { ["userPhoto"] = userPhoto })),
                            Rx.Throw<Unit>(new InvalidOperationException())
                        )
                    )
                ))

            )
            .SubscribeTask();
        }

        public string GetPageContent()
        {
            var content = new System.Text.StringBuilder(Resource.CONTENT_HTML_START);
            content.Append(State.ToString(0));
            content.Append(Resource.CONTENT_HTML_END);
            return content.ToString();
        }

        public async Task SetOrganizationAsync(
            string accessToken,
            string instanceUrl,
            string id,
            string refreshToken)
        {
            await OrgSettings.SaveAysnc(json =>
            {
                var settingForSave = new JObject
                {
                    [OAuth.ACCESS_TOKEN] = accessToken,
                    [OAuth.INSTANCE_URL] = instanceUrl,
                    [OAuth.ID] = id,
                    [OAuth.REFRESH_TOKEN] = refreshToken ?? "",
                };
                json[instanceUrl] = settingForSave;
            }).Continue();
            Commit(new JObject { ["orgSettings"] = new JArray(OrgSettings.List()) });
        }

        public async Task<string> SaveOrgSettingsPathAsync(string newDirectoryPath)
        {
            if (string.IsNullOrEmpty(newDirectoryPath))
            {
                newDirectoryPath = AppContext.BaseDirectory;
            }
            var newFilePath = Path.Combine(newDirectoryPath, AppConstants.ORG_SETTINGS_JSON);

            var oldDirectoryPath = OrgSettings.GetDirectoryPath();
            var oldFilePath = Path.Combine(newDirectoryPath, AppConstants.ORG_SETTINGS_JSON);

            if (newFilePath != oldFilePath)
            {
                try
                {
                    var orgData = OrgSettings.Read();
                    await File.WriteAllTextAsync(newFilePath, orgData.ToString()).Continue();
                    try { File.Delete(oldFilePath); } catch { }
                    await AppSettings.SaveAysnc(o => o[AppConstants.ORG_SETTINGS_PATH] = newDirectoryPath).Continue();
                    OrgSettings.SetPath(newFilePath);
                    Commit(new JObject { [AppConstants.ORG_SETTINGS_PATH] = OrgSettings.GetDirectoryPath() });
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return ex.Message;
                }
            }
            return "No change.";
        }
    }
}
