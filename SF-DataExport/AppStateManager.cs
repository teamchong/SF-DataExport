using DotNetForce;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using SF_DataExport.Dispatcher;
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
                    var payload = action["payload"];

                    switch (type)
                    {
                        case "fetchDirPath":
                            return new FetchDirPath().Dispatch(payload, this);
                        case "fetchPath":
                            return new FetchPath().Dispatch(payload, this);
                        case "loginAsUser":
                            return new LoginAsUser().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                        case "viewPage":
                            return new ViewPage().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                        case "viewUserPage":
                            return new ViewUserPage().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                        case "viewDownloadExports":
                            return new ViewDownloadExports().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                        case "downloadExports":
                            return new DownloadExports().Dispatch(payload, this, Resource, OrgSettings);
                        case "removeOrg":
                            return new RemoveOrg().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                        case "removeOfflineAccess":
                            return new RemoveOfflineAccess().Dispatch(payload, this, OrgSettings);
                        case "saveConfig":
                            return new SaveConfig().Dispatch(payload, this, AppSettings, OrgSettings);
                        case "setOrgSettingsPath":
                            return new SetOrgSettingsPath().Dispatch(payload, this);
                        case "attemptLogin":
                            return new AttemptLogin().Dispatch(payload, this, Resource, OrgSettings);
                        default:
                            Commit(new JObject { [type] = action["payload"] });
                            break;
                    }
                }
            }
            return (JToken)null;
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

        SemaphoreSlim Throttler = new SemaphoreSlim(1, 1);

        public void Commit(JObject newState)
        {
            Observable.FromAsync(async () =>
            {
                if (newState != null)
                {
                    try
                    {
                        await Throttler.WaitAsync().Continue();
                        State.Merge(newState, MergeSettings);
                        CommitSubject.OnNext(newState);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        Throttler.Release();
                    }
                }
            }).SubscribeOn(CurrentThreadScheduler.Instance).Subscribe();
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
