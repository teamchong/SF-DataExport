using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
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
        OrgConnections Organizations { get; set; }

        public AppDialog(string orgPath)
        {
            Resource = new ResourceHelper();
            Organizations = new OrgConnections(orgPath);
            SfLogoUri = "data:image/x-icon;base64," + Convert.ToBase64String(Resource.GetResourceBytes("favicon.ico"));
        }

        public JObject GetInitialAppState()
        {
            var appState = new JObject
            {
                ["appPage"] = "index",
                ["organizations"] = new JArray(Organizations.List()),
                ["organization"] = null,
                ["showOrganization"] = false,
                ["isLoading"] = false,
                ["attemptingDomain"] = "",
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
                page.ExposeFunctionAsync("subscribeAction", (JObject action, JObject state) =>
                {
                    return SubscribeAction(action, state, page, appState);
                }),
                page.ExposeFunctionAsync("subscribeMutation", (JObject mutation, JObject state) =>
                {
                    var commits = SubscribeMutation(mutation, state, page, appState);
                    appState = state;
                    return commits;
                }),
                Task.Run(() =>
                {
                    page.Error += Page_Error;
                    page.PageError += Page_PageError;
                    //page.Console += Page_Console;
                    //page.Response += (object sender, ResponseCreatedEventArgs e) => Page_Response(e.Response, page, appState);
                    page.Request += (object sender, RequestEventArgs e) => Page_Request(e.Request, page, appState);
                    //page.RequestFinished += (object sender, RequestEventArgs e) => Page_RequestFinished(e.Request, page, appState);
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
            Console.WriteLine("Response: " + response.Url);
        }

        void Page_Request(Request request, Page page, JObject appState)
        {
            switch (request.Url)
            {
                case var url when IsAuthorizationPage(url):
                    page.SetRequestInterceptionAsync(true);
                    page.SetJavaScriptEnabledAsync(false);
                    break;

                case OAuth.REDIRECT_URI:
                case var url when url == OAuth.REDIRECT_URI + '/':
                    page.SetRequestInterceptionAsync(true); // safe guard

                    if (IsAuthorizationPage(page.Url))
                    {
                        PageInterception(() => request.AbortAsync(), page);
                    }
                    else
                    {
                        appState["isLoading"] = false;
                        PageInterception(() => request.RespondAsync(new ResponseData
                        {
                            Status = System.Net.HttpStatusCode.Created,
                            ContentType = "text/html",
                            Body = GetPageContent(appState, datauri: false)
                        }), page);
                    }
                    break;
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "/assets/"):
                    var resPath = request.Url.Substring((OAuth.REDIRECT_URI + "/assets/").Length);
                    var res = Resource.GetResourceBytes(resPath);
                    if (res == null)
                        PageInterception(() => request.AbortAsync(), page);
                    else
                        PageInterception(() => request.RespondAsync(new ResponseData
                        {
                            Status = System.Net.HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(resPath),
                            BodyData = res
                        }), page);
                    break;
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "/fonts/"):
                    var fntPath = request.Url.Substring((OAuth.REDIRECT_URI + "/").Length);
                    var fnt = Resource.GetResourceBytes(fntPath);
                    if (fnt == null)
                        PageInterception(() => request.AbortAsync(), page);
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
            Console.WriteLine("Finished " + request.Url);
        }

        private void Page_RequestFailed(Request request, Page page, JObject appState)
        {
            Console.WriteLine("Failed " + request.Url);
        }

        private void Page_DOMContentLoaded(Page page, JObject appState)
        {
            switch (page.Url)
            {
                case var url when IsAuthorizationPage(url):
                    Observable.FromAsync(async () =>
                    {
                        var content = await page.GetContentAsync();
                        await page.SetRequestInterceptionAsync(true);
                        await page.SetJavaScriptEnabledAsync(true);
                        var tokensEncoded = Regex.Match(content, Regex.Escape(OAuth.REDIRECT_URI) + "/?[\\?#]([^'\"]+)")?.Groups[1].Value;
                        if (tokensEncoded != null)
                        {
                            var tokens = HttpUtility.ParseQueryString(tokensEncoded);
                            var loginSetting = new JObject();
                            foreach (var key in tokens.AllKeys)
                            {
                                loginSetting[key] = tokens[key];
                            }
                            loginSetting["login_url"] = appState["attemptingDomain"];

                            if (new[] { "access_token", "instance_url" }.All(s => !string.IsNullOrEmpty(loginSetting[s]?.ToString())))
                            {
                                await Organizations.SaveAysnc(json =>
                                {
                                    json[loginSetting["instance_url"]] = loginSetting;
                                    appState["organizations"] = new JArray(json.Properties().Select(p => p.Name));
                                    appState["organization"] = loginSetting["instance_url"];
                                    appState["showOrganization"] = false;
                                });
                            }
                            else if (new[] { "error", "error_description" }.All(s => !string.IsNullOrEmpty(loginSetting[s]?.ToString())))
                            {
                                Console.WriteLine($"Login fail ({loginSetting["error"]})\n${loginSetting["error_description"]}");
                            }
                            else
                            {
                                Console.WriteLine($"Login fail (Unknown)\n${loginSetting}");
                            }
                        }
                        appState["attemptingDomain"] = "";
                    })
                    .Catch((Exception ex) => Observable.Timer(TimeSpan.FromSeconds(1)).SelectMany(_ => Observable.Throw<System.Reactive.Unit>(ex)))
                    .Retry(3)
                    .SelectMany(_ => Observable.FromAsync(() => page.GoToAsync(OAuth.REDIRECT_URI)))
                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                    //default:
                    //    Console.WriteLine("ContentLoaded " + Page.Url);
                    //    break;
            }
        }

        JObject SubscribeMutation(JObject mutation, JObject state, Page page, JObject appState)
        {
            return null;
        }

        JObject SubscribeAction(JObject action, JObject state, Page page, JObject appState)
        {
            switch (action?["type"]?.ToString())
            {
                case "@attemptLogin":
                    var attemptingDomain = action?["payload"]?.ToString();
                    if (!string.IsNullOrEmpty(attemptingDomain))
                    {
                        var url = "https://" + attemptingDomain + "/services/oauth2/authorize" +
                            "?response_type=token" +
                            "&client_id=" + HttpUtility.UrlEncode(OAuth.CLIENT_ID) +
                            "&redirect_uri=" + HttpUtility.UrlEncode(OAuth.REDIRECT_URI) +
                            "&display=popup";
                        Observable.Concat(
                            Observable.FromAsync(() => page.SetRequestInterceptionAsync(false)),
                            Observable.FromAsync(() => page.GoToAsync(url)).Cast<System.Reactive.Unit>()
                        ).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                        appState["isLoading"] = true;
                        appState["attemptingDomain"] = attemptingDomain;
                    }
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
                $"--disable-web-security",
                $"--user-agent=\"SF-DataExport\"",
                $"--enable-features=NetworkService",
                $"--app=\"{loadingPage}\"",
                $"--start-maximized",
                $"--ignore-certificate-errors"
            }) };
            return launchOpts;
        }

        bool IsAuthorizationPage(string url)
        {
            return url.EndsWith("/_ui/identity/oauth/ui/AuthorizationPage");
        }

        public void PageInterception(Func<Task> func, Page page)
        {
            Observable.FromAsync(func)
            .Catch((Exception ex) => Observable.Concat(
                Observable.FromAsync(() => page.SetRequestInterceptionAsync(true)),
                Observable.Timer(TimeSpan.FromSeconds(1)).SelectMany(_ => Observable.Throw<System.Reactive.Unit>(ex))
            )).Retry(3)
            .Catch((Exception ex) => Observable.Empty<System.Reactive.Unit>())
            .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }
    }
}
