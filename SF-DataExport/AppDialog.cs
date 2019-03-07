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
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Rx = System.Reactive.Linq.Observable;
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

        public static async Task<AppDialog> CreateAsync(JsonConfig appSettings, JsonConfig orgSettings, ResourceManager resource, 
            string instanceUrl, JObject command)
        {
            var chromePath = appSettings.GetString(AppConstants.CHROME_PATH);

            var closeSubject = new Subject<bool>();
            var browser = await Puppeteer.LaunchAsync(GetLaunchOptions(chromePath, command?["command"]?.ToString()));
            try
            {
                browser.Closed += (object sender, EventArgs e) => closeSubject.OnCompleted();
                var appPage = await Rx.FromAsync(() => browser.PagesAsync()).Select(p => p.Length > 0 ? p[0] : null);

                var appState = new AppStateManager(appPage , appSettings, orgSettings, resource);
                appState.Value["currentInstanceUrl"] = instanceUrl;

                var appDialog = new AppDialog(resource, appSettings, orgSettings, appState, appPage);
                await appDialog.PageSetupAsync(appPage, true);

                await appDialog.AppPage.GoToAsync(OAuth.REDIRECT_URI);

                await closeSubject.Count();
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
                try { if (!browser?.Process?.HasExited != true) browser?.Process?.Kill(); } catch { }
            }
        }

        static LaunchOptions GetLaunchOptions(string chromePath, string command)
        {
            var favIcon = "<link rel='shortcut icon' type='image/x-icon' href='/assets/images/favicon.ico'>";
            var loadingPage = "data:text/html,<title>Loading SF DataLoader...</title>" + favIcon;

            var launchOpts = new LaunchOptions();
            launchOpts.ExecutablePath = chromePath;
            launchOpts.Headless = !string.IsNullOrEmpty(command);
            launchOpts.DefaultViewport = null;
            launchOpts.IgnoreHTTPSErrors = true;
            launchOpts.Args = new string[] { string.Join(" ", new [] {
                $"--force-app-mode",
                $"--disable-extensions",
                $"--enable-experimental-accessibility-features",
                $"--no-sandbox",
                $"--disable-web-security",
                $"--user-agent=\"dotnetforce\"",
                $"--enable-features=NetworkService",
                $"--app=\"{loadingPage}\"",
                $"--start-maximized",
                $"--ignore-certificate-errors"
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
        }

        async Task<Page> PageSetupAsync(Page page, bool interception)
        {
            await Task.WhenAll(
                page.SetRequestInterceptionAsync(interception),
                page.SetCacheEnabledAsync(false),
                page.SetBypassCSPAsync(true),
                page.ExposeFunctionAsync("subscribeDispatch", (JArray actions) =>
                {
                    AppState.SubscribeDispatch(actions);
                    return (JToken)null;
                }),
                Task.Run(() =>
                {
                    page.Error += Page_Error;
                    page.PageError += Page_PageError;
                    page.Console += Page_Console;
                    //page.Response += Page_Response;
                    page.Request += Page_Request;
                    page.RequestFinished += Page_RequestFinished;
                    page.RequestFailed += Page_RequestFailed;
                    page.DOMContentLoaded += Page_DOMContentLoaded;
                })
            );
            return page;
        }

        void PageInterception(Func<Task> func, Request request)
        {
            Rx.Concat(
                Rx.If(() => request.Method != HttpMethod.Get,
                Rx.FromAsync(() => AppPage.SetRequestInterceptionAsync(false))),
                Rx.FromAsync(func)
            )
            .Catch((Exception ex) => Rx.Empty<Unit>())
            .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        void Page_Error(object sender, PuppeteerSharp.ErrorEventArgs e) => Console.WriteLine("Error: " + e.Error);

        void Page_PageError(object sender, PageErrorEventArgs e) => Console.WriteLine("PageError: " + e.Message);

        void Page_Console(object sender, ConsoleEventArgs e)
        {
            Observable.FromAsync(async () =>
            {
                var messages = new List<string>();
                foreach (var arg in e.Message.Args)
                {
                    var message = JsonConvert.SerializeObject(await arg.JsonValueAsync(), Formatting.Indented);
                    if (!string.IsNullOrEmpty(message) && message != "null" && message != "{}") messages.Add(message);
                }
                Console.WriteLine(
                "Console:" + e.Message.Type.ToString() + "\n" +
                "URL: " + e.Message.Location.URL + "\n" +
                "Line: " + e.Message.Location.LineNumber + "\n" +
                "Column: " + e.Message.Location.ColumnNumber + "\n" +
                (messages.Count > 0 ? string.Join(Environment.NewLine, messages) : ""));
            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        void Page_Response(object sender, ResponseCreatedEventArgs e) => Console.WriteLine("Response: " + e.Response.Url);

        void Page_Request(object sender, RequestEventArgs e)
        {
            switch (e.Request.Url)
            {
                case OAuth.REDIRECT_URI:
                case OAuth.REDIRECT_URI_SANDBOX:
                    Rx.FromAsync(async () =>
                    {
                        await AppPage.SetRequestInterceptionAsync(true);
                        PageInterception(() => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.Created,
                            ContentType = "text/html",
                            Body = AppState.GetPageContent()
                        }), e.Request);
                    })
                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                case var url when Resource.IsLoginUrl(url) && url.Contains(".salesforce.com/assets/icons/"):
                    var icoPath = "icons/" + e.Request.Url.Split(".salesforce.com/assets/icons/", 2).Last();
                    var ico = Resource.GetResourceBytes(icoPath);
                    if (ico == null)
                        PageInterception(() => e.Request.ContinueAsync(), e.Request);
                    else
                        PageInterception(() => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(icoPath),
                            BodyData = ico
                        }), e.Request);
                    break;
                case var url when Resource.IsLoginUrl(url) && url.Contains(".salesforce.com/assets/images/"):
                    var imgPath = "images/" + e.Request.Url.Split(".salesforce.com/assets/images/", 2).Last();
                    if (imgPath == "images/favicon.ico")
                    {
                        PageInterception(() => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = "image/x-icon",
                            BodyData = Resource.GetResourceBytes("favicon.ico")
                        }), e.Request);
                    }
                    else
                    {
                        var img = Resource.GetResourceBytes(imgPath);
                        if (img == null)
                            PageInterception(() => e.Request.ContinueAsync(), e.Request);
                        else
                            PageInterception(() => e.Request.RespondAsync(new ResponseData
                            {
                                Status = HttpStatusCode.OK,
                                ContentType = Resource.GetContentType(imgPath),
                                BodyData = img
                            }), e.Request);
                    }
                    break;
                case var url when Resource.IsLoginUrl(url) && url.Contains(".salesforce.com/services/fonts/"):
                    var fntPath = "fonts/" + e.Request.Url.Split(".salesforce.com/services/fonts/", 2).Last();
                    var fnt = Resource.GetResourceBytes(fntPath);
                    if (fnt == null)
                        PageInterception(() => e.Request.ContinueAsync(), e.Request);
                    else
                        PageInterception(() => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(fntPath),
                            BodyData = fnt
                        }), e.Request);
                    break;
                default:
                    PageInterception(() => e.Request.ContinueAsync(), e.Request);
                    break;
            }
        }

        void Page_RequestFinished(object sender, RequestEventArgs e)
        {
            switch (AppPage.Target.Url)
            {
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#"):
                    Rx.FromAsync(async () =>
                    {
                        var tokens = HttpUtility.ParseQueryString(url.Split(new[] { '#' }, 2).Last());
                        if (tokens.Count > 0)
                        {
                            var newOrg = new JObject();
                            foreach (var key in tokens.AllKeys)
                            {
                                newOrg[key] = tokens[key];
                            }
                            newOrg[OAuth.LOGIN_URL] = tokens["state"];

                            if (new[] { OAuth.ACCESS_TOKEN, OAuth.INSTANCE_URL }.All(s => !string.IsNullOrEmpty(newOrg[s]?.ToString())))
                            {
                                var client = new DNFClient(newOrg[OAuth.INSTANCE_URL]?.ToString(), newOrg[OAuth.ACCESS_TOKEN]?.ToString(), newOrg[OAuth.REFRESH_TOKEN]?.ToString());

                                try
                                {
                                    await client.TokenRefreshAsync(new Uri(newOrg[OAuth.LOGIN_URL]?.ToString()), Resource.GetClientIdByLoginUrl(newOrg[OAuth.LOGIN_URL]?.ToString()));
                                    await AppState.SetOrganizationAsync(
                                        newOrg[OAuth.ACCESS_TOKEN]?.ToString(),
                                        newOrg[OAuth.INSTANCE_URL]?.ToString(),
                                        newOrg[OAuth.LOGIN_URL]?.ToString(),
                                        newOrg[OAuth.ID]?.ToString(),
                                        newOrg[OAuth.REFRESH_TOKEN]?.ToString());
                                    AppState.Commit(AppState.GetOrgSettings());
                                    AppState.SetCurrentInstanceUrl(client);
                                }
                                catch (Exception ex)
                                {
                                    AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                                    Console.WriteLine($"Login fail (REST)\n${ex.Message}");
                                    AppState.PageAlert($"Login fail (REST)\n${ex.Message}");
                                }
                            }
                            else if (new[] { "error", "error_description" }.All(s => !string.IsNullOrEmpty(newOrg[s]?.ToString())))
                            {
                                AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                                Console.WriteLine($"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}");
                                AppState.PageAlert($"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}");
                            }
                            else
                            {
                                AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                                Console.WriteLine($"Login fail (Unknown)\n${newOrg}");
                                AppState.PageAlert($"Login fail (Unknown)\n${newOrg}");
                            }
                        }
                        else
                        {
                            AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                        }
                        return Resource.GetRedirectUrlByLoginUrl(url);
                    })
                    .SelectMany(redirectUrl => Rx.FromAsync(() => 
                        AppPage.EvaluateExpressionHandleAsync("location.replace(" + JsonConvert.SerializeObject(redirectUrl) + ")")))
                        .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                || url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    Rx.FromAsync(() => AppPage.GoToAsync(OAuth.REDIRECT_URI))
                        .SelectMany(_ => Rx.FromAsync(() => AppPage.ReloadAsync()))
                        .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
        }

        void Page_RequestFailed(object sender, RequestEventArgs e)
        {
            switch (AppPage.Target.Url)
            {
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                || url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    Rx.FromAsync(() => AppPage.GoToAsync(OAuth.REDIRECT_URI))
                        .SelectMany(_ => Rx.FromAsync(() => AppPage.ReloadAsync()))
                        .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
        }

        void Page_DOMContentLoaded(object sender, EventArgs e)
        {
            switch (AppPage.Url)
            {
                case var url when url.Contains("/ui/setup/export/DataExportPage/d?setupid=DataManagementExport"):

                    Rx.FromAsync(() => AppPage.QuerySelectorAllAsync("tr.dataRow"))
                    .Select(els => els.ToObservable()).Concat()
                    .Select(el => Rx.FromAsync(async () =>
                    {
                        var a = (await el.QuerySelectorAsync("a:eq(0)"));
                        var href = await a.GetPropertiesAsync();
                        var th = (await el.QuerySelectorAsync("th:eq(1)"));
                        var filename = await th.GetPropertiesAsync();
                        return a;
                    })).Concat()
                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
        }

    }
}
