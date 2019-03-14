using DotNetForce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Unit = System.Reactive.Unit;

namespace SF_DataExport
{
    public class AppDialog
    {
        ResourceManager Resource { get; set; }
        JsonConfig AppSettings { get; set; }
        JsonConfig OrgSettings { get; set; }
        AppStateManager AppState { get; set; }
        Page AppPage { get; set; }
        bool IsRequestInterception { get; set; }

        public static async Task<AppDialog> CreateAsync(JsonConfig appSettings, JsonConfig orgSettings, ResourceManager resource,
            string instanceUrl, JObject command)
        {
            var chromePath = appSettings.GetString(AppConstants.PATH_CHROME);

            var appState = new AppStateManager(appSettings, orgSettings, resource);
            var exist = await appState.ProcessCommandAsync(command);
            if (exist)
            {
                return null;
            }
            //appState.Commit(new JObject { ["currentInstanceUrl"] = instanceUrl });
            var browser = await Puppeteer.LaunchAsync(GetLaunchOptions(chromePath, (string)command?["command"])).GoOn();

            try
            {
                var isClose = new Subject<bool>();
                browser.Closed += (object sender, EventArgs e) => isClose.OnCompleted();
                var appPage = (await browser.PagesAsync().GoOn()).FirstOrDefault();

                var appDialog = new AppDialog(resource, appSettings, orgSettings, appState, appPage);
                await appState.SubscribeAsync(appPage).GoOn();
                appState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                await isClose.Count().SubscribeOn(TaskPoolScheduler.Default);
                return appDialog;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw ex;
            }
            finally
            {
                try { await browser?.CloseAsync(); } catch { }
                try { if (browser?.Process?.HasExited != true) browser?.Process?.Kill(); } catch { }
            }
        }

        static LaunchOptions GetLaunchOptions(string chromePath, string command)
        {
            var favIcon = "<link rel='shortcut icon' type='image/x-icon' href='https://login.salesforce.com/favicon.ico'>";
            var loadingPage = "data:text/html,<title>Loading SF DataLoader...</title>" + favIcon;

            var launchOpts = new LaunchOptions();
            launchOpts.ExecutablePath = chromePath;
            launchOpts.Headless = !string.IsNullOrEmpty(command);
            launchOpts.DefaultViewport = null;
            launchOpts.IgnoreHTTPSErrors = true;
            launchOpts.DumpIO = false;
            launchOpts.Args = new string[] { string.Join(" ", new [] {
                $"--force-app-mode",
                $"--disable-extensions",
                //$"--enable-experimental-accessibility-features",
                //$"--no-sandbox",
                //$"--disable-web-security",
                $"--user-agent=\"dotnetforce\"",
                //$"--enable-features=NetworkService",
                $"--app=\"{loadingPage}\"",
                $"--start-maximized",
                //$"--ignore-certificate-errors"
            }) };
            return launchOpts;
        }

        private AppDialog(ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings, AppStateManager appState, Page appPage)
        {
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            AppState = appState;
            AppPage = appPage;

            appPage.Error += Page_Error;
            appPage.PageError += Page_PageError;
            appPage.Console += Page_Console;
            //appPage.Response += Page_Response
            appPage.Request += Page_Request;
            appPage.RequestFinished += Page_RequestFinished;
            appPage.RequestFailed += Page_RequestFailed;
            //appPage.DOMContentLoaded += Page_DOMContentLoaded;
        }



        IObservable<Unit> PageInterception(Page appPage, Func<Task> funcAsync, Request request)
        {
            return Observable.FromAsync(async () =>
            {
                if (request.Method != HttpMethod.Get)
                {
                    await appPage.SetRequestInterceptionAsync(false).GoOn();
                    IsRequestInterception = false;
                }
                else if (IsRequestInterception)
                {
                    await funcAsync().GoOn();
                }
            })
            .Catch((Exception ex) => Observable.Defer(() =>
            {
                Console.WriteLine(ex.ToString());
                return Observable.Return(Unit.Default);
            }))
            .SubscribeOn(TaskPoolScheduler.Default);
        }

        void Page_Error(object sender, PuppeteerSharp.ErrorEventArgs e)
        {
            Console.WriteLine("Error: " + e.Error);
        }

        void Page_PageError(object sender, PageErrorEventArgs e)
        {
            Console.WriteLine("PageError: " + e.Message);
        }

        async void Page_Console(object sender, ConsoleEventArgs e)
        {
            await Observable.FromAsync(async () =>
            {
                var messages = new List<string>();
                if (e.Message.Args != null)
                {
                    foreach (var arg in e.Message.Args)
                    {
                        var message = (string)(await arg.GetPropertyAsync("message").GoOn())?.RemoteObject?.Value;
                        if (!string.IsNullOrEmpty(message))
                        {
                            messages.Add(message);
                        }
                        else
                        {
                            var json = await arg.JsonValueAsync().GoOn();
                            messages.Add(JsonConvert.SerializeObject(json, Formatting.Indented));
                        }
                    }
                }
                Console.WriteLine(
                    "Console:" + e.Message.Type.ToString() + "\n" +
                    "URL: " + e.Message.Location.URL + "\n" +
                    "Line: " + e.Message.Location.LineNumber + "\n" +
                    "Column: " + e.Message.Location.ColumnNumber + "\n" +
                    (messages.Count > 0 ? string.Join(Environment.NewLine, messages) : ""));
            }).Catch((Exception ex) => Observable.Return(Unit.Default))
            .SubscribeOn(TaskPoolScheduler.Default);
        }

        void Page_Response(object sender, ResponseCreatedEventArgs e)
        {
            Console.WriteLine("Response: " + e.Response?.Url);
        }

        async void Page_Request(object sender, RequestEventArgs e)
        {
            var appPage = sender as Page;
            if (appPage == null) return;

            switch (e.Request.Url)
            {
                case OAuth.REDIRECT_URI:
                case OAuth.REDIRECT_URI_SANDBOX:
                    await Observable.FromAsync(async () =>
                    {
                        await appPage.SetRequestInterceptionAsync(true).GoOn();
                        IsRequestInterception = true;
                    })
                        .SelectMany(_ => PageInterception(appPage, () => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.Created,
                            ContentType = "text/html",
                            Body = AppState.GetPageContent(),
                            Headers = new Dictionary<string, object> { ["Cache-Control"] = "no-store" },
                        }), e.Request))
                        .SubscribeOn(TaskPoolScheduler.Default);
                    break;
                case var url when Resource.IsLoginUrl(url) && url.Contains(".salesforce.com/assets/icons/"):
                    var iconPath = "icons/" + e.Request.Url.Split(".salesforce.com/assets/icons/", 2).Last();
                    var icon = Resource.GetResourceBytes(iconPath);
                    if (icon != null)
                        await PageInterception(appPage, () => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(iconPath),
                            BodyData = icon
                        }), e.Request);
                    else
                        await PageInterception(appPage, () => e.Request.ContinueAsync(), e.Request);
                    break;
                case var url when Resource.IsLoginUrl(url) && url.Contains(".salesforce.com/assets/images/"):
                    var imgPath = "images/" + e.Request.Url.Split(".salesforce.com/assets/images/", 2).Last();
                    var img = Resource.GetResourceBytes(imgPath);
                    if (img != null)
                        await PageInterception(appPage, () => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(imgPath),
                            BodyData = img
                        }), e.Request);
                    else
                        await PageInterception(appPage, () => e.Request.ContinueAsync(), e.Request);
                    break;
                case var url when Resource.IsLoginUrl(url) && url.Contains(".salesforce.com/fonts/"):
                    var fntPath = "fonts/" + e.Request.Url.Split(".salesforce.com/fonts/", 2).Last();
                    var fnt = Resource.GetResourceBytes(fntPath);
                    if (fnt != null)
                        await PageInterception(appPage, () => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(fntPath),
                            BodyData = fnt
                        }), e.Request);
                    else
                        await PageInterception(appPage, () => e.Request.ContinueAsync(), e.Request);
                    break;
                case var url when Resource.IsLoginUrl(url) && url.Count(c => c == '/') == 3 && !url.EndsWith('/'):
                    var path = e.Request.Url.Split('/').LastOrDefault();
                    var file = Resource.GetResource(path);
                    if (file != null)
                        await PageInterception(appPage, () => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(path),
                            Body = file
                        }), e.Request);
                    else
                        await PageInterception(appPage, () => e.Request.ContinueAsync(), e.Request);
                    break;
                case var url when url.Contains(".content.force.com/profilephoto/"):
                    await Observable.FromAsync(async () =>
                    {
                        var instanceUrl = AppState.Value["currentInstanceUrl"]?.ToString();
                        if (string.IsNullOrEmpty(instanceUrl))
                            await PageInterception(appPage, () => e.Request.ContinueAsync(), e.Request);
                        else
                        {
                            var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]);
                            var bytes = await Resource.GetBytesViaAccessTokenAsync(instanceUrl, accessToken, e.Request.Url);
                            if (bytes?.LongLength > 0)
                                await PageInterception(appPage, () => e.Request.RespondAsync(new ResponseData
                                {
                                    Status = HttpStatusCode.OK,
                                    ContentType = "image/png",
                                    BodyData = bytes
                                }), e.Request);
                            else
                                await PageInterception(appPage, () => e.Request.ContinueAsync(), e.Request);
                        }
                    })
                    .SubscribeOn(TaskPoolScheduler.Default);
                    break;
                case var url when !string.IsNullOrEmpty((string)AppState.Value["currentInstanceUrl"]) && url.StartsWith((string)AppState.Value["currentInstanceUrl"]):
                    Resource.OpenIncognitoBrowser((string)AppState.Value["currentInstanceUrl"], url.Substring(((string)AppState.Value["currentInstanceUrl"]).Length),
                        AppSettings, OrgSettings);
                    break;
                default:
                    await PageInterception(appPage, () => e.Request.ContinueAsync(), e.Request);
                    break;
            }
        }

        async void Page_RequestFinished(object sender, RequestEventArgs e)
        {
            var appPage = sender as Page;
            switch (appPage?.Target.Url)
            {
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#"):
                    await Observable.FromAsync(async () =>
                    {
                        var tokens = HttpUtility.ParseQueryString(url.Split(new[] { '#' }, 2).Last());
                        if (tokens.Count > 0)
                        {
                            var newOrg = new JObject();
                            foreach (var key in tokens.AllKeys)
                            {
                                newOrg[key] = tokens[key];
                            }

                            if (new[] { OAuth.ACCESS_TOKEN, OAuth.INSTANCE_URL }.All(s => !string.IsNullOrEmpty((string)newOrg[s])))
                            {
                                var client = new DNFClient((string)newOrg[OAuth.INSTANCE_URL], (string)newOrg[OAuth.ACCESS_TOKEN], (string)newOrg[OAuth.REFRESH_TOKEN]);

                                try
                                {
                                    var loginUrl = Resource.GetLoginUrl(newOrg[OAuth.ID]);
                                    await client.TokenRefreshAsync(new Uri(loginUrl), Resource.GetClientIdByLoginUrl(loginUrl)).GoOn();
                                    await AppState.SetOrganizationAsync(
                                        (string)newOrg[OAuth.ACCESS_TOKEN],
                                        (string)newOrg[OAuth.INSTANCE_URL],
                                        (string)newOrg[OAuth.ID],
                                        (string)newOrg[OAuth.REFRESH_TOKEN]
                                    ).GoOn();
                                    AppState.Commit(AppState.GetOrgSettings());
                                    AppState.SetCurrentInstanceUrl(client);
                                }
                                catch (Exception ex)
                                {
                                    AppState.Commit(new JObject
                                    {
                                        ["currentAccessToken"] = "",
                                        ["currentId"] = "",
                                        ["currentInstanceUrl"] = "",
                                        ["showOrgModal"] = true,
                                        ["alertMessage"] = $"Login fail (REST)\n${ex.Message}"
                                    });
                                    Console.WriteLine($"Login fail (REST)\n${ex.Message}");
                                }
                            }
                            else if (new[] { "error", "error_description" }.All(s => !string.IsNullOrEmpty((string)newOrg[s])))
                            {
                                AppState.Commit(new JObject
                                {
                                    ["currentAccessToken"] = "",
                                    ["currentId"] = "",
                                    ["currentInstanceUrl"] = "",
                                    ["showOrgModal"] = true,
                                    ["alertMessage"] = $"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}"
                                });
                                Console.WriteLine($"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}");
                            }
                            else
                            {
                                AppState.Commit(new JObject
                                {
                                    ["currentAccessToken"] = "",
                                    ["currentId"] = "",
                                    ["currentInstanceUrl"] = "",
                                    ["showOrgModal"] = true,
                                    ["alertMessage"] = $"Login fail (Unknown)\n${newOrg}"
                                });
                                Console.WriteLine($"Login fail (Unknown)\n${newOrg}");
                            }
                        }
                        else
                        {
                            AppState.Commit(new JObject
                            {
                                ["currentAccessToken"] = "",
                                ["currentId"] = "",
                                ["currentInstanceUrl"] = "",
                                ["showOrgModal"] = true
                            });
                        }
                        return Resource.GetRedirectUrlByLoginUrl(url);
                    })
                    .SelectMany(redirectUrl =>
                        Observable.Defer(() =>
                        {
                            AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = redirectUrl });
                            return Observable.Return(Unit.Default);
                        })
                    ).Catch((Exception ex) => Observable.Return(Unit.Default))
                    .SubscribeOn(TaskPoolScheduler.Default);
                    break;
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                    break;
            }
        }

        void Page_RequestFailed(object sender, RequestEventArgs e)
        {
            var appPage = sender as Page;
            Console.WriteLine("RequestFailed: " + e.Request.Url);
            switch (appPage?.Target.Url)
            {
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                    || url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                    break;
            }
        }

        void Page_DOMContentLoaded(object sender, EventArgs e)
        {
            //var appPage = sender as Page;
            //switch (appPage?.Url)
            //{
            //}
        }
    }
}
