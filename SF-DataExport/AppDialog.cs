using Microsoft.Extensions.DependencyInjection;
using DotNetForce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using SF_DataExport.Dispatcher;
using SF_DataExport.Interceptor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Web;
using Unit = System.Reactive.Unit;

namespace SF_DataExport
{
    public class AppDialog
    {
        string ChromePath { get; }
        JObject Command { get; }
        AppStateManager AppState { get; }
        List<InterceptorBase> Interceptors { get; }

        public AppDialog(string chromePath, JObject command, AppStateManager appState, List<InterceptorBase> interceptors)
        {
            ChromePath = chromePath;
            Command = command;
            AppState = appState;
            Interceptors = interceptors;
        }

        public async Task ShowAsync()
        {
            //AppState.Commit(new JObject { ["currentInstanceUrl"] = instanceUrl });
            var browser = await Puppeteer.LaunchAsync(GetLaunchOptions(ChromePath)).GoOn();

            try
            {
                var isClose = new Subject<bool>();
                browser.Closed += (object sender, EventArgs e) => isClose.OnCompleted();
                var appPage = (await browser.PagesAsync().GoOn()).FirstOrDefault();

                appPage.Error += Page_Error;
                appPage.PageError += Page_PageError;
                appPage.Console += Page_Console;
                appPage.Response += Page_Response;
                appPage.Request += Page_Request;
                appPage.RequestFinished += Page_RequestFinished;
                appPage.RequestFailed += Page_RequestFailed;
                appPage.DOMContentLoaded += Page_DOMContentLoaded;

                await AppState.SubscribeAsync(appPage).GoOn();
                AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                if (Command != null)
                {
                    await AppState.DispatchActionsAsync(appPage, new JArray(Command));
                }
                await isClose.LastOrDefaultAsync().SubscribeOn(TaskPoolScheduler.Default);
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

        static LaunchOptions GetLaunchOptions(string chromePath)
        {
            var favIcon = "<link rel='shortcut icon' type='image/x-icon' href='https://login.salesforce.com/favicon.ico'>";
            var loadingPage = "data:text/html,<title>Loading SF DataLoader...</title>" + favIcon;

            var launchOpts = new LaunchOptions();
            launchOpts.ExecutablePath = chromePath;
            launchOpts.Headless = false;
            launchOpts.DefaultViewport = null;
            launchOpts.IgnoreHTTPSErrors = false;
            launchOpts.DumpIO = false;
            launchOpts.Args = new string[] { string.Join(" ", new [] {
                $"--force-app-mode",
                //$"--disable-extensions",
                $"--bwsi", //Indicates that the browser is in "browse without sign-in" (Guest session) mode. Should completely disable extensions, sync and bookmarks. 
                $"--no-first-run",
                $"--disable-default-apps",
                $"--disable-dev-shm-usage",
                $"--disable-crash-reporter",
                $"--disable-breakpad",
                //$"--disable-gpu",
                $"--no-sandbox",
                //$"--no-experiments",
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

        async void Page_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                var appPage = sender as Page;
                if (appPage != null)
                {
                    foreach (var interceptor in Interceptors)
                    {
                        var func = interceptor.ErrorAsync(appPage, e.Error);

                        if (func != null && await func.GoOn())
                        {
                            return;
                        }
                    }

                    Console.WriteLine("Error: " + e.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async void Page_PageError(object sender, PageErrorEventArgs e)
        {
            try
            {
                var appPage = sender as Page;
                if (appPage != null)
                {
                    foreach (var interceptor in Interceptors)
                    {
                        var func = interceptor.PageErrorAsync(appPage, e.Message);

                        if (func != null && await func.GoOn())
                        {
                            return;
                        }
                    }

                    Console.WriteLine("PageError: " + e.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async void Page_Console(object sender, ConsoleEventArgs e)
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async void Page_Response(object sender, ResponseCreatedEventArgs e)
        {
            try
            {
                var appPage = sender as Page;
                if (appPage != null)
                {
                    foreach (var interceptor in Interceptors)
                    {
                        var func = interceptor.ResponseAsync(appPage, e.Response);

                        if (func != null && await func.GoOn())
                        {
                            return;
                        }
                    }

                    //Console.WriteLine("Response: " + e.Response?.Url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async void Page_Request(object sender, RequestEventArgs e)
        {
            try
            {
                var appPage = sender as Page;
                if (appPage != null)
                {
                    foreach (var interceptor in Interceptors)
                    {
                        var func = interceptor.RequestAsync(appPage, e.Request);

                        if (func != null && await func.GoOn())
                        {
                            return;
                        }
                    }

                    await AppState.IntercepObservable(appPage, e.Request, () => e.Request.ContinueAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async void Page_RequestFinished(object sender, RequestEventArgs evt)
        {
            try
            {
                var appPage = sender as Page;
                if (appPage != null)
                {
                    foreach (var interceptor in Interceptors)
                    {
                        var func = interceptor.RequestFinishedAsync(appPage, evt.Request);

                        if (func != null && await func.GoOn())
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async void Page_RequestFailed(object sender, RequestEventArgs evt)
        {
            try
            {
                var appPage = sender as Page;
                if (appPage != null)
                {
                    foreach (var interceptor in Interceptors)
                    {
                        var func = interceptor.RequestFailedAsync(appPage, evt.Request);

                        if (func != null && await func.GoOn())
                        {
                            return;
                        }
                    }
                    Console.WriteLine("RequestFailed: " + evt.Request.Url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        async void Page_DOMContentLoaded(object sender, EventArgs evt)
        {
            try
            {
                var appPage = sender as Page;
                if (appPage != null)
                {
                    foreach (var interceptor in Interceptors)
                    {
                        var func = interceptor.DOMContentLoadedAsync(appPage);

                        if (func != null && await func.GoOn())
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
