using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Unit = System.Reactive.Unit;

namespace SF_DataExport
{
    public class AppDialog
    {
        string ChromePath { get; }
        JObject Command { get; }
        AppStore AppState { get; }
        IObservable<InterceptorBase> Interceptors { get; }

        public AppDialog(string chromePath, JObject command, AppStore store, IObservable<InterceptorBase> interceptors)
        {
            ChromePath = chromePath;
            Command = command;
            AppState = store;
            Interceptors = interceptors;
        }

        public async Task ShowAsync()
        {
            //AppState.Commit(new JObject { ["currentInstanceUrl"] = instanceUrl });
            var browser = await Puppeteer.LaunchAsync(GetLaunchOptions(ChromePath)).GoOn();

            try
            {
                var isClose = new Subject<bool>();
                Observable.FromEventPattern(h => browser.Closed += h, h => browser.Closed -= h)
                    .Take(1)
                    .Subscribe(evt => isClose.OnCompleted());

                var appPage = (await browser.PagesAsync().GoOn()).FirstOrDefault();

                Observable.FromEventPattern<ErrorEventArgs>(h => appPage.Error += h, h => appPage.Error -= h)
                    .SelectMany((EventPattern<ErrorEventArgs> evt) => 
                        Interceptors.Select(interceptor => interceptor.ErrorAsync((Page)evt.Sender, evt.EventArgs.Error))
                        .SelectMany(task => task != null ? Observable.FromAsync(() => task).SuppressErrors() : Observable.Empty<Unit>())
                    )
                    .Subscribe();
                Observable.FromEventPattern<PageErrorEventArgs>(h => appPage.PageError += h, h => appPage.PageError -= h)
                    .SelectMany((EventPattern<PageErrorEventArgs> evt) => 
                        Interceptors.Select(interceptor => interceptor.PageErrorAsync((Page)evt.Sender, evt.EventArgs.Message))
                        .SelectMany(task => task != null ? Observable.FromAsync(() => task).SuppressErrors() : Observable.Empty<Unit>())
                    )
                    .Subscribe();
                Observable.FromEventPattern<ResponseCreatedEventArgs>(h => appPage.Response += h, h => appPage.Response -= h)
                    .SelectMany((EventPattern<ResponseCreatedEventArgs> evt) =>
                        Interceptors.Select(interceptor => interceptor.ResponseAsync((Page)evt.Sender, evt.EventArgs.Response))
                        .SelectMany(task => task != null ? Observable.FromAsync(() => task).SuppressErrors() : Observable.Empty<Unit>())
                    )
                    .Subscribe();
                Observable.FromEventPattern<RequestEventArgs>(h => appPage.Request += h, h => appPage.Request -= h)
                    .SelectMany((EventPattern<RequestEventArgs> evt) => 
                        Interceptors.Select(interceptor => interceptor.RequestAsync((Page)evt.Sender, evt.EventArgs.Request))
                        .SelectMany(task => task != null ? Observable.FromAsync(() => task).SuppressErrors() : Observable.Return(false))
                        .Count(intercepted => intercepted)
                        .Where(interceptedCount => interceptedCount <= 0)
                        .SelectMany(_interceptedCount => Observable.FromAsync(() => evt.EventArgs.Request.ContinueAsync()))
                    )
                    .Subscribe();
                Observable.FromEventPattern<RequestEventArgs>(h => appPage.RequestFinished += h, h => appPage.RequestFinished -= h)
                    .SelectMany((EventPattern<RequestEventArgs> evt) => 
                        Interceptors.Select(interceptor => interceptor.RequestFinishedAsync((Page)evt.Sender, evt.EventArgs.Request))
                        .SelectMany(task => task != null ? Observable.FromAsync(() => task).SuppressErrors() : Observable.Empty<Unit>())
                    )
                    .Subscribe();
                Observable.FromEventPattern<RequestEventArgs>(h => appPage.RequestFailed += h, h => appPage.RequestFailed -= h)
                    .SelectMany((EventPattern<RequestEventArgs> evt) => 
                        Interceptors.Select(interceptor => interceptor.RequestFailedAsync((Page)evt.Sender, evt.EventArgs.Request))
                        .SelectMany(task => task != null ? Observable.FromAsync(() => task).SuppressErrors() : Observable.Empty<Unit>())
                    )
                    .Subscribe();
                Observable.FromEventPattern(h => appPage.DOMContentLoaded += h, h => appPage.DOMContentLoaded -= h)
                    .SelectMany((EventPattern<object> evt) => 
                        Interceptors.Select(interceptor => interceptor.DOMContentLoadedAsync((Page)evt.Sender))
                        .SelectMany(task => task != null ? Observable.FromAsync(() => task).SuppressErrors() : Observable.Empty<Unit>())
                    )
                    .Subscribe();
#if DEBUG
                Observable.FromEventPattern<ConsoleEventArgs>(h => appPage.Console += h, h => appPage.Console -= h)
                    .SelectMany(evt => Observable.FromAsync(() => LogPageConsoleAsync(evt.EventArgs)).SuppressErrors())
                    .Subscribe();
#endif

                await AppState.SubscribeAsync(appPage).GoOn();
                AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                if (Command != null)
                {
                    await AppState.ReduceActionsAsync(appPage, new JArray(Command));
                }
                await isClose.LastOrDefaultAsync().ToTask().GoOn();
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

#if DEBUG
        async Task LogPageConsoleAsync(ConsoleEventArgs evt)
        {
            var messages = new List<string>();
            if (evt.Message.Args != null)
            {
                foreach (var arg in evt.Message.Args)
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
                "Console:" + evt.Message.Type.ToString() + "\n" +
                "URL: " + evt.Message.Location.URL + "\n" +
                "Line: " + evt.Message.Location.LineNumber + "\n" +
                "Column: " + evt.Message.Location.ColumnNumber + "\n" +
                (messages.Count > 0 ? string.Join(Environment.NewLine, messages) : ""));
        }
#endif
    }
}
