using Newtonsoft.Json;
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
using StringBuilder = System.Text.StringBuilder;
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

        JsonConfig AppSettings { get; set; }
        JsonConfig OrgSettings { get; set; }
        ResourceManager Resource { get; set; }
        JObject Command { get; set; }

        JObject State { get; set; }
        Subject<JObject> CommitSubject = new Subject<JObject>();

        public JObject Value { get => State; }

        public AppStateManager(JsonConfig appSettings, JsonConfig orgSettings, ResourceManager resource, JObject command)
        {
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            Resource = resource;
            Command = command;
            State = new JObject
            {
                ["alertMessage"] = "",
                ["chromePath"] = AppSettings.GetString(AppConstants.PATH_CHROME),
                ["chromePathItems"] = new JArray(AppSettings.GetString(AppConstants.PATH_CHROME)),
                ["cmdExport"] = "dotnet " + AppDomain.CurrentDomain.FriendlyName + ".dll download@",
                ["currentAccessToken"] = "",
                ["currentId"] = "",
                ["currentInstanceUrl"] = "",
                ["exportCount"] = null,
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
                ["userPicture"] = "",
                ["userThumbnail"] = "",
                ["users"] = new JArray(),
            };
            State.Merge(GetOrgSettings(), MergeSettings);
            ProcessCommand(command);
        }

        public void ProcessCommand(JObject command)
        {
            if (command == null) return;

            switch ((string)command?["command"])
            {
                case AppConstants.COMMAND_DOWNLOAD:
                    Observable.Merge(
                        Observable.Start(() => new DownloadExports().Dispatch(command, this, Resource, OrgSettings)).Select(_ => 0L),
                        Observable.Start(() =>
                        {
                            var exportCount = (long?)State["exportCount"];
                            if (exportCount != null)
                            {
                                var exportResultFiles = (JObject)State["exportResultFiles"];
                                var pending = exportResultFiles.PropertyValues().Select(p => (string)p)
                                    .Count(r => !r.StartsWith("Downloaded....") && !r.StartsWith("Skipped....") && !r.StartsWith("Failed...."));
                                if (pending <= 0)
                                {
                                    return Observable.Throw<long>(new EndOfStreamException());
                                }
                            }
                            return Observable.Timer(TimeSpan.FromSeconds(10));
                        }).Concat().Repeat()
                        .Catch((EndOfStreamException ex) => Observable.Empty<long>())
                    )
                    .Catch((Exception ex) => Observable.Start(() => Console.WriteLine(ex.ToString())).Select(_ => 0L))
                    .SubscribeOn(ImmediateScheduler.Instance)
                    .Subscribe();
                    break;
            }
        }

        public void DispatchActions(Page appPage, JArray actions)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i] as JObject;
                if (action != null)
                {
                    var type = (string)action["type"];
                    var payload = action["payload"];

                    switch (type)
                    {
                        case "attemptLogin":
                            new AttemptLogin().Dispatch(payload, this, Resource, OrgSettings);
                            break;
                        case "downloadExports":
                            new DownloadExports().Dispatch(payload, this, Resource, OrgSettings);
                            break;
                        case "fetchDirPath":
                            new FetchDirPath().Dispatch(payload, this);
                            break;
                        case "fetchPath":
                            new FetchPath().Dispatch(payload, this);
                            break;
                        case "loginAsUser":
                            new LoginAsUser().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                            break;
                        case "saveConfig":
                            new SaveConfig().Dispatch(payload, this, AppSettings, OrgSettings);
                            break;
                        case "setOrgSettingsPath":
                            new SetOrgSettingsPath().Dispatch(payload, this);
                            break;
                        case "switchUser":
                            new SwitchUser().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                            break;
                        case "removeOfflineAccess":
                            new RemoveOfflineAccess().Dispatch(payload, this, OrgSettings);
                            break;
                        case "removeOrg":
                            new RemoveOrg().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                            break;
                        case "viewDownloadExports":
                            new ViewDownloadExports().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                            break;
                        case "viewPage":
                            new ViewPage().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                            break;
                        case "viewUserPage":
                            new ViewUserPage().Dispatch(payload, this, Resource, AppSettings, OrgSettings);
                            break;
                        default:
                            Commit(new JObject { [type] = payload });
                            break;
                    }
                }
            }
        }

        public async Task SubscribeAsync(Page appPage)
        {
            await appPage.SetRequestInterceptionAsync(true).GoOn();
            await appPage.SetCacheEnabledAsync(false).GoOn();
            await appPage.SetBypassCSPAsync(true).GoOn();
            await appPage.ExposeFunctionAsync("subscribeDispatch", (JArray actions) =>
                {
                    if (Resource.IsRedirectPage(appPage.Url))
                    {
                        DispatchActions(appPage, actions);
                    }
                    return (JToken)null;
                }).GoOn();
            CommitSubject.SelectMany((JObject newState) => Observable.Defer(() =>
                {
                    var redirect = (string)newState?[AppConstants.ACTION_REDIRECT];
                    if (!string.IsNullOrEmpty(redirect))
                    {
                        return Observable.FromAsync(async () =>
                        {
                            await appPage.GoToAsync(redirect, 0, new[] { WaitUntilNavigation.DOMContentLoaded }).GoOn();
                            newState.Remove(AppConstants.ACTION_REDIRECT);
                            return newState;
                        });
                    }
                    return Observable.Return(newState);
                }))
                .Where(state => state?.HasValues == true)
                .Buffer(TimeSpan.FromMilliseconds(100))
                .Where(newStates => newStates.Any())
                .SelectMany(newStates =>
                    Observable.If(() => Resource.IsRedirectPage(appPage.Url),
                        Observable.FromAsync(() =>
                        {
                            //var expression = string.Join("",
                            //    "if(typeof storeCommit!=='undefined'){try{storeCommit([",
                            //    string.Join(",", newStates.Select(newState => newState.ToString(0))),
                            //    "])}catch(_){}}");
                            //return appPage.EvaluateExpressionAsync(expression.ToString());
                            return appPage.EvaluateFunctionHandleAsync("storeCommit", newStates);
                        }),
                        Observable.Empty<JSHandle>()
                    )
                )
                //.ScheduleTask();
                .Catch((Exception ex) => Observable.Start(() => Console.WriteLine(ex.ToString())).SelectMany(__ => Observable.Empty<JSHandle>()))
                .Finally(() => Console.WriteLine("CommitSubject end unexpctedly."))
                .Subscribe();
            //.SubscribeOn(TaskPoolScheduler.Default).Subscribe();
            //
            //.SubscribeOn(TaskPoolScheduler.Default).Subscribe(_ => Console.WriteLine("next"), err => Console.WriteLine(err.ToString()), () => Console.WriteLine("complete"));
        }

        public JObject GetOrgSettings()
        {
            var orgList = OrgSettings.List();
            var orgOfflineAccess = new JArray(orgList
                    .Where(org => !string.IsNullOrEmpty((string)OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]))));
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
                    State.Remove(AppConstants.ACTION_REDIRECT);
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
            var userId = client.Id.Split('/').Last();
            Commit(new JObject
            {
                ["currentInstanceUrl"] = client.InstanceUrl,
                ["exportUserId"] = "",
                ["popoverUserId"] = "",
                ["showOrgModal"] = false,
                ["userDisplayName"] = "",
                ["userEmail"] = "",
                ["userId"] = userId,
                ["userName"] = "",
                ["userPicture"] = "",
                ["userThumbnail"] = "",
                ["users"] = new JArray()
            });
            Observable.Merge(

                Observable.FromAsync(async () =>
                {
                    var users = new JArray(await client.GetEnumerableAsync("SELECT Id,Name,Email FROM User WHERE IsActive=true ORDER BY Name,Email").GoOn());
                    if ((string)State["currentInstanceUrl"] != client.InstanceUrl)
                        throw new InvalidOperationException();
                    Commit(new JObject { ["users"] = users });
                })
                .Catch(Observable.Empty<Unit>()),

                Observable.FromAsync(() => client.UserInfo())
                .SelectMany(userInfo => Observable.Merge(

                    Observable.If(() => (string)State["currentInstanceUrl"] == client.InstanceUrl && (string)State["userId"] == userId,
                        Observable.Start(() => Commit(new JObject
                        {
                            ["userDisplayName"] = userInfo?["display_name"],
                            ["userEmail"] = userInfo?["email"],
                            ["userName"] = userInfo?["username"],
                        }))
                        .Catch(Observable.Empty<Unit>()),
                        Observable.Empty<Unit>()
                    ),
                    
                    Observable.FromAsync(() => Resource.GetDataUriViaAccessToken(client.InstanceUrl, client.AccessToken,
                        (string)userInfo?["photos"]?["picture"], "image/png"))
                    .SelectMany(userPhoto =>
                        Observable.If(
                            () => (string)State["currentInstanceUrl"] == client.InstanceUrl && (string)State["userId"] == userId,
                            Observable.Start(() => Commit(new JObject { ["userPicture"] = userPhoto })),
                            Observable.Throw<Unit>(new InvalidOperationException())
                        )
                    ),

                    Observable.FromAsync(() => Resource.GetDataUriViaAccessToken(client.InstanceUrl, client.AccessToken,
                        (string)userInfo?["photos"]?["thumbnail"], "image/png"))
                    .SelectMany(userPhoto =>
                        Observable.If(
                            () => (string)State["currentInstanceUrl"] == client.InstanceUrl && (string)State["userId"] == userId,
                            Observable.Start(() => Commit(new JObject { ["userThumbnail"] = userPhoto })),
                            Observable.Throw<Unit>(new InvalidOperationException())
                        )
                    )
                ))

            )
            .ScheduleTask();
        }

        public string GetPageContent()
        {
            //return "<html>test<img src='/assets/images/avatar1.jpg'/><img src='/assets/images/avatar2.jpg'/></html>";
            var content = string.Join("", Resource.CONTENT_HTML_START, State.ToString(0), Resource.CONTENT_HTML_END);
            return content;
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
            }).GoOn();
            Commit(new JObject { ["orgSettings"] = new JArray(OrgSettings.List()) });
        }

        public async Task<string> SaveOrgSettingsPathAsync(string newDirectoryPath)
        {
            if (string.IsNullOrEmpty(newDirectoryPath))
            {
                newDirectoryPath = AppContext.BaseDirectory;
            }
            var newFilePath = Path.Combine(newDirectoryPath, AppConstants.JSON_ORG_SETTINGS);

            var oldDirectoryPath = OrgSettings.GetDirectoryPath();
            var oldFilePath = Path.Combine(newDirectoryPath, AppConstants.JSON_ORG_SETTINGS);

            if (newFilePath != oldFilePath)
            {
                try
                {
                    var orgData = OrgSettings.Read();
                    await File.WriteAllTextAsync(newFilePath, orgData.ToString()).GoOn();
                    try { File.Delete(oldFilePath); } catch { }
                    await AppSettings.SaveAysnc(o => o[AppConstants.PATH_ORG_SETTINGS] = newDirectoryPath).GoOn();
                    OrgSettings.SetPath(newFilePath);
                    Commit(new JObject { [AppConstants.PATH_ORG_SETTINGS] = OrgSettings.GetDirectoryPath() });
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
