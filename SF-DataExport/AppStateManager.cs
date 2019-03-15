using DotNetForce;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using SF_DataExport.Dispatcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

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

        JObject State { get; set; }
        Subject<JObject> CommitSubject = new Subject<JObject>();

        public JObject Value { get => State; }

        public AppStateManager(JsonConfig appSettings, JsonConfig orgSettings, ResourceManager resource)
        {
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            Resource = resource;
            State = new JObject
            {
                ["alertMessage"] = "",
                ["chromePath"] = AppSettings.GetString(AppConstants.PATH_CHROME),
                ["chromePathItems"] = new JArray(AppSettings.GetString(AppConstants.PATH_CHROME)),
                ["cmdExport"] = AppDomain.CurrentDomain.FriendlyName + " download@",
                ["currentAccessToken"] = "",
                ["currentId"] = "",
                ["currentInstanceUrl"] = "",
                ["exportCount"] = null,
                ["exportEmails"] = "",
                ["exportPath"] = Resource.DefaultDirectory.TrimEnd(Path.DirectorySeparatorChar),
                ["exportPathItems"] = new JArray(Resource.DefaultDirectory.TrimEnd(Path.DirectorySeparatorChar)),
                ["exportResult"] = "",
                ["exportResultFiles"] = new JArray(),
                ["isLoading"] = false,
                ["orgLimits"] = new JObject(),
                ["orgSettingsPath"] = OrgSettings.GetDirectoryPath(),
                ["orgSettingsPathItems"] = new JArray(OrgSettings.GetDirectoryPath()),
                ["popoverUserId"] = "",
                ["showOrgModal"] = true,
                ["showUserPopover"] = false,
                ["tab"] = "overview", //"downloaddataexport", //"setup"
                ["userId"] = "",
                ["userIdAs"] = "",
                ["userLicenses"] = new JArray(),
                ["userProfiles"] = new JArray(),
                ["userRoles"] = new JObject(),
                ["users"] = new JArray(),
            };
            State.Merge(GetOrgSettings(), MergeSettings);
        }

        public async Task<bool> ProcessCommandAsync(JObject command)
        {
            if (command == null) return false;

            switch ((string)command?["command"])
            {
                case AppConstants.COMMAND_DOWNLOAD:
                    await Observable.Merge(
                        Observable.Defer(() =>
                        {
                            new DownloadExports().Dispatch(command, this, Resource, OrgSettings);
                            return Observable.Empty<long>();
                        }),
                        Observable.Defer(() =>
                        {
                            var exportCount = (long?)State["exportCount"];
                            if (exportCount != null)
                            {
                                var alertMessage = (string)State["alertMessage"];
                                var exportResult = (string)State["exportResult"];

                                if (!string.IsNullOrEmpty(alertMessage) || exportResult?.Contains("Export completed") == true)
                                {
                                    return Observable.Throw<long>(new EndOfStreamException());
                                }
                            }
                            return Observable.Timer(TimeSpan.FromSeconds(10));
                        }).Repeat()
                    )
                    .Catch((EndOfStreamException ex) => Observable.Empty<long>())
                    .Catch((Exception ex) => Observable.Defer(() =>
                    {
                        Console.WriteLine(ex.ToString());
                        return Observable.Return(0L);
                    }))
                    .Count()
                    .SubscribeOn(TaskPoolScheduler.Default);
                    return true;
            }
            return false;
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
                            new ViewPage().Dispatch((string)payload, this, Resource, AppSettings, OrgSettings);
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
                        })
				        .Catch((Exception ex) => Observable.Defer(() =>
				        {
#if DEBUG
					        Console.WriteLine(ex.ToString());
#endif
					        return Observable.Never<JToken>();
				        }));
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
				    .Catch((Exception ex) => Observable.Defer(() =>
				    {
#if DEBUG
					    Console.WriteLine(ex.ToString());
#endif
					    return Observable.Never<JSHandle>();
				    }))
                )
                //.ScheduleTask();
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

        public JArray GetOrgData(List<JObject> data, string dataParentRoleId, JArray users, string instanceUrl)
        {
            var result = new JArray();
            foreach (var row in data)
            {
                var id = row["Id"]?.ToString() ?? "";
                var name = row["Name"]?.ToString() ?? "";
                var parentRoleId = row["ParentRoleId"]?.ToString() ?? "";
                if (parentRoleId == dataParentRoleId)
                {
                    var obj = new JObject
                    {
                        ["id"] = id,
                        ["name"] = name,
                        ["url"] = !string.IsNullOrEmpty(id) ? instanceUrl + "/" + id : "",
                        ["users"] = new JArray(users.Where(u => (string)u.SelectToken("UserRole.Name") == name)),
                    };
                    var children = GetOrgData(data, id, users, instanceUrl);
                    if (children.Count > 0)
                    {
                        obj["children"] = children;
                    }
                    result.Add(obj);
                }
            }
            return result;
        }

        public void SetCurrentInstanceUrl(DNFClient client)
        {
            var userId = client.Id.Split('/').Last();
            Commit(new JObject
            {
                ["currentInstanceUrl"] = client.InstanceUrl,
                ["popoverUserId"] = "",
                ["showOrgModal"] = false,
                ["orgLimits"] = new JObject(),
                ["userId"] = userId,
                ["userIdAs"] = userId,
                ["userLicenses"] = new JArray(),
                ["userProfiles"] = new JArray(),
                ["userRoles"] = new JObject(),
                ["users"] = new JArray(),
            });

            Observable.FromAsync(async () =>
            {
                if ((string)State["currentInstanceUrl"] != client.InstanceUrl)
                    throw new InvalidOperationException();

                var request = new BatchRequest();
                request.Limits();
                request.Query("SELECT Name,UsedLicenses,TotalLicenses FROM UserLicense WHERE Status = 'Active' AND TotalLicenses > 0 ORDER BY Name");
                request.Query("SELECT Id,Name,Username,Email,UserRole.Name,Profile.Name,FullPhotoUrl,SmallPhotoUrl FROM User WHERE IsActive=true ORDER BY Name,Email");
                request.Query("SELECT Id,Name,ParentRoleId FROM UserRole ORDER BY ParentRoleId,Name");
                var result = await client.Composite.BatchAsync(request);

                var orgLimits = result.Results("0") ?? new JObject();
                var userLicenses = new JArray(client.GetEnumerable(result.Queries("1")));
                var users = new JArray(client.GetEnumerable(result.Queries("2")));
                var userProfiles = new JArray(users
                    .Select(user => (string)user.SelectToken("Profile.Name") ?? "")
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Distinct().OrderBy(r => r));
                var userRoles = new JObject
                {
                    ["id"] = client.InstanceUrl,
                    ["name"] = client.InstanceUrl.Replace("https://", ""),
                    ["url"] = "",
                    ["users"] = new JArray(),
                    ["children"] = GetOrgData(client.GetEnumerable(result.Queries("3")).ToList(), "", users, client.InstanceUrl),
                };

                if ((string)State["currentInstanceUrl"] != client.InstanceUrl)
                    throw new InvalidOperationException();
                Commit(new JObject
                {
                    ["orgLimits"] = orgLimits,
                    ["userLicenses"] = userLicenses,
                    ["userProfiles"] = userProfiles,
                    ["userRoles"] = userRoles,
                    ["users"] = users,
                });
            })
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
                newDirectoryPath = Resource.DefaultDirectory;
            }
            var newFilePath = Path.Combine(newDirectoryPath, AppConstants.JSON_ORG_SETTINGS);

            var oldDirectoryPath = OrgSettings.GetDirectoryPath();
            var oldFilePath = Path.Combine(newDirectoryPath, AppConstants.JSON_ORG_SETTINGS);

            if (newFilePath != oldFilePath)
            {
                try
                {
                    var orgData = OrgSettings.Get(d => d);
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
