using DotNetForce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport
{
    public class AppDialog
    {
        string SfLogoUri { get; set; }

        ResourceHelper Resource { get; set; }
        JsonConfig AppSettings { get; set; }
        JsonConfig OrgSettings { get; set; }


        ConcurrentDictionary<string, DateTime> PostingUrls { get; set; }

        public AppDialog(JsonConfig appSettings, JsonConfig orgSettings)
        {
            Resource = new ResourceHelper();
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            SfLogoUri = "data:image/x-icon;base64," + Convert.ToBase64String(Resource.GetResourceBytes("favicon.ico"));
            PostingUrls = new ConcurrentDictionary<string, DateTime>();
        }

        public string GetClientId(string loginUrl)
        {
            if (loginUrl.Contains("login.salesforce.com"))
                return OAuth.CLIENT_ID;
            else
                return OAuth.CLIENT_ID_SANDBOX;
        }

        public string GetRedirectUrl(string loginUrl)
        {
            if (loginUrl.Contains("login.salesforce.com"))
                return OAuth.REDIRECT_URI;
            else
                return OAuth.REDIRECT_URI_SANDBOX;
        }

        public Task<JToken> Alert(string message, Page page)
        {
            return page.EvaluateExpressionAsync("alert(" + JsonConvert.SerializeObject(message ?? "") + ")");
        }

        public Task<JToken> Confirm(string message, Page page)
        {
            return page.EvaluateExpressionAsync("confirm(" + JsonConvert.SerializeObject(message ?? "") + ")");
        }

        public Task<JToken> Prompt(string message, string defaultValue, Page page)
        {
            return page.EvaluateExpressionAsync("prompt(" + JsonConvert.SerializeObject(message ?? "") +
                "," + JsonConvert.SerializeObject(defaultValue ?? "") + ")");
        }

        public Task<JToken> CommitAsync(JObject changes, Page page)
        {
            if (changes != null)
            {
                try
                {
                    var changeList = new List<string>();
                    foreach (var c in changes.Properties().Select(p => p.Name))
                    {
                        changeList.Add(@"store.commit('commit'," + JsonConvert.SerializeObject(new JObject
                        {
                            ["name"] = c,
                            ["value"] = changes[c]
                        }) + ")");
                    }
                    return page.EvaluateExpressionAsync("try{if(typeof store!=='undefined'){" + string.Join(";", changeList) + "}}catch(_ex){}");
                }
                catch { }
            }
            return Task.FromResult<JToken>(null);
        }

        public JObject GetInitialAppState()
        {
            var appState = new JObject
            {
                ["appPage"] = "index",
                ["attemptingDomain"] = "",
                ["attemptLoginResult"] = null,
                ["currentInstanceUrl"] = "",
                ["isLoading"] = false,
                ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                    .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]?.ToString())))),
                ["orgOpened"] = false,
                ["orgSettings"] = new JArray(OrgSettings.List()),
                ["orgSettingsPath"] = OrgSettings.GetPath(),
                ["salesforceLogoUri"] = SfLogoUri,
            };
            return appState;
        }

        public async Task DisplayAsync(string chromePath)
        {
            var appState = GetInitialAppState();
            var closeSubject = new System.Reactive.Subjects.Subject<bool>();
            var browser = await Puppeteer.LaunchAsync(GetLaunchOptions(chromePath));
            try
            {
                browser.Closed += (object sender, EventArgs e) => closeSubject.OnCompleted();
                var page = await Observable.FromAsync(() => browser.PagesAsync()).Select(p => p.Length > 0 ? p[0] : null)
                    .SelectMany(p => Observable.FromAsync(() => SetupPageAsync(true, p, appState)));

                //await page.SetContentAsync(GetPageContent(appState, datauri: false));
                await page.GoToAsync(OAuth.REDIRECT_URI);
                await closeSubject.Count();
            }
            finally
            {
                try { await browser?.CloseAsync(); } catch { }
                try { if (!browser?.Process?.HasExited != true) browser?.Process?.Kill(); } catch { }
            }
        }

        async Task<Page> SetupPageAsync(bool interception, Page page, JObject appState)
        {
            await Task.WhenAll(
                page.SetRequestInterceptionAsync(interception),
                page.SetCacheEnabledAsync(false),
                page.SetBypassCSPAsync(true),
                page.ExposeFunctionAsync("getState", (string name) =>
                {
                    return appState[name];
                }),
                page.ExposeFunctionAsync("subscribeAction", (JObject action, JObject state) =>
                {
                    return SubscribeAction(action, state, page, appState);
                }),
                page.ExposeFunctionAsync("subscribeMutation", (JObject mutation, JObject state) =>
                {
                    var commits = SubscribeMutation(mutation, state, page, appState);
                    foreach (var name in state.Properties().Select(s => s.Name))
                    {
                        appState[name] = state[name];
                    }
                    return commits;
                }),
                Task.Run(() =>
                {
                    page.Error += Page_Error;
                    page.PageError += Page_PageError;
                    //page.Console += Page_Console;
                    //page.Response += (object sender, ResponseCreatedEventArgs e) => Page_Response(e.Response, page, appState);
                    page.Request += (object sender, RequestEventArgs e) => Page_Request(e.Request, page, appState);
                    page.RequestFinished += (object sender, RequestEventArgs e) => Page_RequestFinished(e.Request, page, appState);
                    page.RequestFailed += (object sender, RequestEventArgs e) => Page_RequestFailed(e.Request, page, appState);
                    page.DOMContentLoaded += (object sender, EventArgs e) => Page_DOMContentLoaded(page, appState);
                })
            );
            return page;
        }

        string GetPageContent(JObject appState, bool datauri)
        {
            return string.Join("", datauri ? "data:text/html," : "", @"<html>
<head>
<title>SF DataLoader</title>
<link rel='shortcut icon' type='image/x-icon' href='", SfLogoUri, @"'>
<style>", Resource.GetResource("slds.css"), @"</style>
<style>[v-cloak]{display:none;}</style>
<script>", Resource.GetResource("vue.js"), @"</script>
<script>", Resource.GetResource("vuex.js"), @"</script>
<script>", Resource.GetResource("rxjs.js"), @"</script>
</head>
<body>
", Resource.GetResource("app.html").Replace("\"<<appState>>\"", JsonConvert.SerializeObject(appState)), @"
</body>
</html>");
        }

        void Page_Error(object sender, PuppeteerSharp.ErrorEventArgs e)
        {
            Console.WriteLine("Error: " + e.Error);
        }

        void Page_PageError(object sender, PageErrorEventArgs e)
        {
            Console.WriteLine("PageError: " + e.Message);
        }

        void Page_Console(object sender, ConsoleEventArgs e)
        {
            Console.WriteLine("Console: " + e.Message);
        }

        void Page_Response(Response response, Page page, JObject appState)
        {
            page.SetRequestInterceptionAsync(true);
            Console.WriteLine("Response: " + response.Url);
        }

        void Page_Request(Request request, Page page, JObject appState)
        {
            switch (request.Url)
            {
                case OAuth.REDIRECT_URI:
                case OAuth.REDIRECT_URI_SANDBOX:
                    page.SetRequestInterceptionAsync(true);
                    appState["isLoading"] = true;
                    PageInterception(() => request.RespondAsync(new ResponseData
                    {
                        Status = System.Net.HttpStatusCode.Created,
                        ContentType = "text/html",
                        Body = GetPageContent(appState, datauri: false)
                    }), page);
                    break;
                case var url when (url.StartsWith("https://login.salesforce.com/") || url.StartsWith("https://test.salesforce.com/")) && url.Contains(".salesforce.com/assets/icons/"):
                    var icoPath = "icons/" + request.Url.Split(".salesforce.com/assets/icons/", 2).Last();
                    var ico = Resource.GetResourceBytes(icoPath);
                    if (ico == null)
                        PageInterception(() => request.ContinueAsync(), page);
                    else
                        PageInterception(() => request.RespondAsync(new ResponseData
                        {
                            Status = System.Net.HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(icoPath),
                            BodyData = ico
                        }), page);
                    break;
                case var url when (url.StartsWith("https://login.salesforce.com/") || url.StartsWith("https://test.salesforce.com/")) && url.Contains(".salesforce.com/assets/images/"):
                    var imgPath = "images/" + request.Url.Split(".salesforce.com/assets/images/", 2).Last();
                    var img = Resource.GetResourceBytes(imgPath);
                    if (img == null)
                        PageInterception(() => request.ContinueAsync(), page);
                    else
                        PageInterception(() => request.RespondAsync(new ResponseData
                        {
                            Status = System.Net.HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(imgPath),
                            BodyData = img
                        }), page);
                    break;
                case var url when (url.StartsWith("https://login.salesforce.com/") || url.StartsWith("https://test.salesforce.com/")) && url.Contains(".salesforce.com/services/fonts/"):
                    var fntPath = "fonts/" + request.Url.Split(".salesforce.com/services/fonts/", 2).Last();
                    var fnt = Resource.GetResourceBytes(fntPath);
                    if (fnt == null)
                        PageInterception(() => request.ContinueAsync(), page);
                    else
                        PageInterception(() => request.RespondAsync(new ResponseData
                        {
                            Status = System.Net.HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(fntPath),
                            BodyData = fnt
                        }), page);
                    break;
                default:
                    //Console.WriteLine("Request " + request.Url);
                    Observable.FromAsync(() => request.ContinueAsync())
                    .Catch((Exception ex) => Observable.Empty<System.Reactive.Unit>())
                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
        }

        void Page_RequestFinished(Request request, Page page, JObject appState)
        {
            switch (page.Target.Url)
            {
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#"):
                    Observable.FromAsync(async () =>
                    {
                        await page.SetRequestInterceptionAsync(true);
                        var tokens = HttpUtility.ParseQueryString(url.Split(new[] { '#' }, 2).Last());
                        if (tokens.Count > 0)
                        {
                            var newOrg = new JObject();
                            foreach (var key in tokens.AllKeys)
                            {
                                newOrg[key] = tokens[key];
                            }
                            newOrg[OAuth.LOGIN_URL] = appState["attemptingDomain"];

                            if (new[] { OAuth.ACCESS_TOKEN, OAuth.INSTANCE_URL }.All(s => !string.IsNullOrEmpty(newOrg[s]?.ToString())))
                            {
                                await SetOrganizationAsync(page, appState,
                                    newOrg[OAuth.ACCESS_TOKEN]?.ToString(),
                                    newOrg[OAuth.INSTANCE_URL]?.ToString(),
                                    newOrg[OAuth.LOGIN_URL]?.ToString(),
                                    newOrg[OAuth.REFRESH_TOKEN]?.ToString());
                                appState["orgOfflineAccess"] = new JArray(OrgSettings.List()
                                    .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]?.ToString()))));
                                appState["orgSettings"] = new JArray(OrgSettings.List());
                                appState["currentInstanceUrl"] = newOrg[OAuth.INSTANCE_URL] ?? "";
                            }
                            else if (new[] { "error", "error_description" }.All(s => !string.IsNullOrEmpty(newOrg[s]?.ToString())))
                            {
                                appState["currentInstanceUrl"] = "";
                                Console.WriteLine($"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}");
                            }
                            else
                            {
                                appState["currentInstanceUrl"] = "";
                                Console.WriteLine($"Login fail (Unknown)\n${newOrg}");
                            }
                        }
                        else
                        {
                            appState["currentInstanceUrl"] = "";
                        }

                        appState["attemptingDomain"] = "";
                    })
                    .SelectMany(redirectUrl => Observable.FromAsync(() => page.GoToAsync(OAuth.REDIRECT_URI)))
                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                || url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    Observable.FromAsync(() => page.GoToAsync(OAuth.REDIRECT_URI))
                        .SelectMany(_ => Observable.FromAsync(() => page.ReloadAsync()))
                        .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                    //default:
                    //    Console.WriteLine("Finished " + request.Url);
                    //    break;
            }
        }

        void Page_RequestFailed(Request request, Page page, JObject appState)
        {
            switch (page.Target.Url)
            {
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                || url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    Observable.FromAsync(() => page.GoToAsync(OAuth.REDIRECT_URI))
                        .SelectMany(_ => Observable.FromAsync(() => page.ReloadAsync()))
                        .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                    //default:
                    //    Console.WriteLine("Failed " + request.Url);
                    //    break;
            }
        }

        void Page_DOMContentLoaded(Page page, JObject appState)
        {
            //switch (page.Url)
            //{
            //    case OAuth.REDIRECT_URI:
            //    case OAuth.REDIRECT_URI_SANDBOX:
            //    case var url when url == OAuth.REDIRECT_URI + '/' || url == OAuth.REDIRECT_URI_SANDBOX + '/':
            //        //appState["isLoading"] = true;
            //        // break;
            //        //default:
            //        //    Console.WriteLine("ContentLoaded " + Page.Url);
            //        //    break;
            //}
        }

        async Task SetOrganizationAsync(
            Page page,
            JObject appState,
            string accessToken,
            string instanceUrl,
            string loginUrl,
            string refreshToken)
        {
            await OrgSettings.SaveAysnc(json =>
            {
                var settingForSave = new JObject
                {
                    [OAuth.ACCESS_TOKEN] = accessToken,
                    [OAuth.INSTANCE_URL] = instanceUrl,
                    [OAuth.LOGIN_URL] = loginUrl,
                    [OAuth.REFRESH_TOKEN] = refreshToken ?? "",
                };
                json[instanceUrl] = settingForSave;
            });
            await CommitAsync(new JObject { ["orgSettings"] = OrgSettings.Read() }, page);
        }

        JObject SubscribeMutation(JObject mutation, JObject state, Page page, JObject appState)
        {
            return null;
        }

        JObject SubscribeAction(JObject action, JObject state, Page page, JObject appState)
        {
            switch (action?["type"]?.ToString())
            {
                case "@removeOrg":
                    Observable.FromAsync(async () =>
                    {
                        var instanceUrl = action["payload"]?.ToString() ?? "";
                        await OrgSettings.SaveAysnc(json =>
                        {
                            if (json[instanceUrl] != null)
                            {
                                json.Remove(instanceUrl);
                            }
                        });
                        await CommitAsync(new JObject
                        {
                            ["currentInstanceUrl"] = appState["currentInstanceUrl"]?.ToString() != instanceUrl ? appState["currentInstanceUrl"] : "",
                            ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                                .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]?.ToString())))),
                            ["orgSettings"] = new JArray(OrgSettings.List()),
                        }, page);
                    }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                case "@removeOfflineAccess":
                    Observable.FromAsync(async () =>
                    {
                        var instanceUrl = action["payload"]?.ToString() ?? "";
                        await OrgSettings.SaveAysnc(json =>
                        {
                            if (json[instanceUrl] != null)
                            {
                                json[instanceUrl][OAuth.REFRESH_TOKEN] = "";
                            }
                        });
                        await CommitAsync(new JObject
                        {
                            ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                                .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]?.ToString())))),
                        }, page);
                    }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                case "@changeOrgSettingsPath":
                    Observable.FromAsync(async () =>
                    {
                        var newPath = action["payload"]?.ToString() ?? "";
                        var oldPath = OrgSettings.GetPath();
                        try
                        {
                            var orgData = OrgSettings.Read();
                            await File.WriteAllTextAsync(newPath, orgData.ToString());
                            try { File.Delete(oldPath); } catch { }
                            OrgSettings.SetPath(newPath);
                            await CommitAsync(new JObject { ["orgSettingsPath"] = OrgSettings.GetPath() }, page);
                        }
                        catch { }
                    }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                case "@attemptLogin":
                    Observable.FromAsync(async () =>
                    {
                        var attemptingDomain = Regex.Replace(action?["payload"]?.ToString() ?? "", "^https?://", "");
                        if (!string.IsNullOrEmpty(attemptingDomain))
                        {
                            if (attemptingDomain != "login.salesforce.com" && attemptingDomain != "test.salesforce.com")
                            {
                                var instanceUrl = "https://" + attemptingDomain;
                                var savedOrg = OrgSettings.Get(o => o[instanceUrl]);
                                if (savedOrg != null)
                                {
                                    var accessToken = savedOrg[OAuth.ACCESS_TOKEN]?.ToString() ?? "";
                                    var refreshToken = savedOrg[OAuth.REFRESH_TOKEN]?.ToString() ?? "";
                                    var loginUrl = savedOrg[OAuth.LOGIN_URL]?.ToString() ?? "";
                                    var client = new DNFClient(instanceUrl, accessToken, refreshToken);

                                    if (loginUrl != "" && loginUrl != attemptingDomain)
                                    {
                                        attemptingDomain = loginUrl;
                                    }

                                    if (refreshToken != "")
                                    {
                                        try
                                        {
                                            await client.TokenRefreshAsync(new Uri(loginUrl), GetClientId(loginUrl));
                                            await SetOrganizationAsync(page, appState,
                                                client.AccessToken,
                                                client.InstanceUrl,
                                                loginUrl,
                                                client.RefreshToken);
                                            await CommitAsync(new JObject { ["currentInstanceUrl"] = client.InstanceUrl }, page);
                                            return;
                                        }
                                        catch
                                        {

                                        }
                                    }
                                    else if (accessToken != "")
                                    {
                                        try
                                        {
                                            var userInfo = await client.UserInfo();
                                            if (userInfo != null)
                                            {
                                                return;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            var url = "https://" + attemptingDomain + "/services/oauth2/authorize" +
                                "?response_type=token" +
                                "&client_id=" + HttpUtility.UrlEncode(GetClientId(attemptingDomain)) +
                                "&redirect_uri=" + HttpUtility.UrlEncode(GetRedirectUrl(attemptingDomain)) +
                                "&display=popup";
                            PostingUrls.TryAdd(url, DateTime.Now);
                            Observable.Concat(
                                Observable.FromAsync(() => page.SetRequestInterceptionAsync(false)),
                                Observable.FromAsync(() => page.GoToAsync(url)).Cast<System.Reactive.Unit>()
                            ).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            await CommitAsync(new JObject
                            {
                                ["isLoading"] = true,
                                ["attemptingDomain"] = attemptingDomain,
                            }, page);
                        }
                    }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
            return null;
        }

        LaunchOptions GetLaunchOptions(string chromePath)
        {
            var favIcon = "<link rel='shortcut icon' type='image/x-icon' href='" + SfLogoUri + "'>";
            var loadingPage = "data:text/html,<title>Loading SF DataLoader...</title>" + favIcon;

            var launchOpts = new LaunchOptions();
            launchOpts.ExecutablePath = chromePath;
            launchOpts.Headless = false;
            launchOpts.DefaultViewport = null;
            launchOpts.IgnoreHTTPSErrors = true;
            launchOpts.Args = new string[] { string.Join(" ", new [] {
                $"--force-app-mode",
                $"--disable-extensions",
                $"--enable-experimental-accessibility-features",
                $"--no-sandbox",
                $"--disable-web-security",
                $"--user-agent=\"SF-DataExport\"",
                $"--enable-features=NetworkService",
                $"--app=\"{loadingPage}\"",
                $"--start-maximized",
                $"--ignore-certificate-errors"
            }) };
            return launchOpts;
        }

        public void PageInterception(Func<Task> func, Page page)
        {
            Observable.FromAsync(func)
            //.Catch((Exception ex) => Observable.Concat(
            //    Observable.FromAsync(() => page.SetRequestInterceptionAsync(true)),
            //    Observable.Timer(TimeSpan.FromSeconds(1)).SelectMany(_ => Observable.Throw<System.Reactive.Unit>(ex))
            //)).Retry(3)
            .Catch((Exception ex) => Observable.Empty<System.Reactive.Unit>())
            .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }
    }
}
