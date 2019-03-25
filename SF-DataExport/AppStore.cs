using DotNetForce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using SF_DataExport.Reducers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport
{
    public class AppStore
    {
        AppSettingsConfig AppSettings { get; }
        OrgSettingsConfig OrgSettings { get; }
        ResourceManager Resource { get; }
        Dictionary<string, Func<JToken, Task<JToken>>> Reducers { get; }
        ConcurrentDictionary<string, JToken> State { get; }
        Subject<JObject> CommitSubject { get; }

        public AppStore(AppSettingsConfig appSettings, OrgSettingsConfig orgSettings, ResourceManager resource, Dictionary<string, Func<JToken, Task<JToken>>> reducers)
        {
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            Resource = resource;
            State = new ConcurrentDictionary<string, JToken>
            {
                ["alertMessage"] = "",
                ["chromePath"] = AppSettings.GetString(AppConstants.PATH_CHROME),
                ["cmd"] = AppDomain.CurrentDomain.FriendlyName,
                ["currentAccessToken"] = "",
                ["currentId"] = "",
                ["currentInstanceUrl"] = "",
                ["exportCount"] = null,
                ["exportEmails"] = "",
                ["exportPath"] = Resource.DefaultDirectory.TrimEnd(Path.DirectorySeparatorChar),
                ["exportResult"] = "",
                ["exportResultFiles"] = new JArray(),
                ["isLoading"] = false,
                ["globalSearch"] = null,
                ["healthCheckTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["objects"] = new JArray(),
                ["orgLimits"] = new JArray(),
                ["orgLimitsLog"] = new JArray(),
                ["orgChartSearch"] = "",
                ["orgSettingsPath"] = OrgSettings.GetDirectoryPath(),
                ["popoverUserId"] = "",
                ["showLimitsModal"] = false,
                ["showOrgModal"] = true,
                ["showPhotosModal"] = false,
                ["showUserPopover"] = false,
                ["tab"] = "overview", //"downloaddataexport", //"setup"
                ["toolingObjects"] = new JArray(),
                ["userId"] = "",
                ["userIdAs"] = "",
                ["userProfiles"] = new JArray(),
                ["userRoles"] = new JObject(),
                ["users"] = new JArray(),
            };
            CommitSubject = new Subject<JObject>();
            MergeState(GetOrgSettings());
            Reducers = reducers;
        }
        

        public List<string> ListState()
        {
            return State.Keys.ToList();
        }

        public JToken GetState(string propertyName)
        {
            return State.TryGetValue(propertyName, out var value) ? value : null;
        }

        public void MergeState(JObject newState)
        {
            if (newState != null)
            {
                foreach (var prop in newState.Properties())
                {
                    if (prop.Name != AppConstants.ACTION_REDIRECT)
                    {
                        State[prop.Name] = prop.Value;
                    }
                }
            }
        }

        public async Task<DNFClient> OfflineAccessAsync(string instanceUrl)
        {
            if (string.IsNullOrEmpty(instanceUrl))
                throw new ArgumentException("instanceUrl is empty");

            Resource.ResetCookie();

            var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]);
            var refreshToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.REFRESH_TOKEN]);
            var loginUrl = Resource.GetLoginUrl(OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID]));
            var client = new DNFClient(instanceUrl, accessToken, refreshToken);
            await client.TokenRefreshAsync(new Uri(loginUrl), Resource.GetClientIdByLoginUrl(loginUrl)).GoOn();
            await SetOrganizationAsync(
                client.AccessToken,
                client.InstanceUrl,
                client.Id,
                client.RefreshToken
            ).GoOn();
            await SetCurrentInstanceUrlAync(client, GetOrgSettings()).GoOn();
            await Resource.GetCookieAsync(client.InstanceUrl, client.AccessToken).GoOn();
            return client;
        }

        public async Task<bool> ProcessCommandAsync(JObject command)
        {
            if (command == null) return false;

            switch ((string)command?["command"])
            {
                case AppConstants.COMMAND_DOWNLOAD:
                    await OfflineAccessAsync((string)command["instanceUrl"] ?? "");
                    await Observable.Merge(
                        Observable.FromAsync(async () =>
                        {
                            await Reducers[nameof(DownloadExports)](command);
                            return 0L;
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
                    .SuppressErrors()
                    .LastOrDefaultAsync().ToTask().GoOn();
                    return true;
                case AppConstants.COMMAND_LOGIN_AS:
                    await OfflineAccessAsync((string)command["instanceUrl"] ?? "");
                    await Reducers[nameof(LoginAsUser)](command);
                    return true;
                case AppConstants.COMMAND_LOG_LIMITS:
                    await OfflineAccessAsync((string)command["instanceUrl"] ?? "");
                    await Reducers[nameof(GetLimits)](command);
                    return true;
            }
            return false;
        }

        public async Task<JToken> ReduceActionsAsync(Page appPage, JArray actions)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i] as JObject;
                if (action != null)
                {
                    var type = (string)action["type"];
                    var payload = action["payload"];

                    if (Reducers.TryGetValue(type, out var dispatcher))
                    {
                        return await dispatcher(payload);
                    }
                    Commit(new JObject { [type] = payload });
                }
            }
            return null;
        }

        public async Task SubscribeAsync(Page appPage)
        {
            await appPage.SetRequestInterceptionAsync(true).GoOn();
            await appPage.SetCacheEnabledAsync(false).GoOn();
            //await appPage.SetBypassCSPAsync(true).GoOn();
            await appPage.ExposeFunctionAsync("subscribeDispatch", async (JArray actions) =>
                {
                    try
                    {
                        if (Resource.IsAppPage(appPage.Url))
                        {
                            return await ReduceActionsAsync(appPage, actions).GoOn();
                        }
                    }
#if DEBUG
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
#else
                    catch
                    {
                    }
#endif
                    return null;
                }).GoOn();
            CommitSubject.Select((JObject newState) => Observable.Defer(() =>
                {
                    var redirect = (string)newState?[AppConstants.ACTION_REDIRECT];
                    if (!string.IsNullOrEmpty(redirect))
                    {
                        return Observable.FromAsync(async () =>
                        {
                            if (Resource.IsAppPage(redirect))
                            {
                                await appPage.EvaluateExpressionHandleAsync("location.replace(" + HttpUtility.JavaScriptStringEncode(redirect, true) + ")").GoOn();
                                await appPage.WaitForNavigationAsync(new NavigationOptions { Timeout = 0, WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).GoOn();
                            }
                            else
                            {
                                await appPage.GoToAsync(redirect, new NavigationOptions { Timeout = 0, WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded } }).GoOn();
                            }
                            newState.Remove(AppConstants.ACTION_REDIRECT);
                            return newState;
                        })
                        .SuppressErrors();
                    }
                    return Observable.Return(newState);
                }))
                .Merge()
                .Where(state => state?.HasValues == true)
                .Buffer(TimeSpan.FromMilliseconds(50))
                .Where(newStates => newStates.Any())
                .Select(newStates => Observable.If(() => Resource.IsAppPage(appPage.Url),
                    Observable.FromAsync(() => appPage.EvaluateFunctionHandleAsync("storeCommit", newStates)).OnErrorResumeNext(Observable.Empty<JSHandle>()),
                    Observable.Empty<JSHandle>()
                ))
                .Merge()
                .Finally(() => Console.WriteLine("CommitSubject end unexpctedly."))
                .Subscribe();
            //.SubscribeOn(TaskPoolScheduler.Default).Subscribe();
            //
            //.SubscribeOn(TaskPoolScheduler.Default).Subscribe(_ => Console.WriteLine("next"), err => Console.WriteLine(err.ToString()), () => Console.WriteLine("complete"));
        }

        public JObject GetOrgSettings()
        {
            var orgList = OrgSettings.List().OrderBy(o => o);
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
                    MergeState(newState);
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

        public Task SetCurrentInstanceUrlAync(DNFClient client)
        {
            return SetCurrentInstanceUrlAync(client, new JObject());
        }

        public async Task SetCurrentInstanceUrlAync(DNFClient client, JObject newState)
        {
            var userId = client.Id.Split('/').Last();
            newState["currentInstanceUrl"] = client.InstanceUrl;
            newState["popoverUserId"] = "";
            newState["showLimitsModal"] = false;
            newState["showOrgModal"] = false;
            newState["showPhotosModal"] = false;
            newState["objects"] = new JArray();
            newState["orgLimits"] = new JArray();
            newState["orgLimitsLog"] = new JArray();
            newState["toolingObjects"] = new JArray();
            newState["userId"] = userId;
            newState["userIdAs"] = userId;
            newState["userProfiles"] = new JArray();
            newState["userRoles"] = new JObject();
            newState["users"] = new JArray();
            Commit(newState);

            await Observable.Merge(
                Observable.FromAsync(async () =>
                {
                    if ((string)State["currentInstanceUrl"] != client.InstanceUrl)
                        throw new InvalidOperationException();

                    var request = new CompositeRequest();
                    request.Query("users", "SELECT Id,Name,Username,Email,UserRole.Name,Profile.Name,FullPhotoUrl,SmallPhotoUrl FROM User WHERE IsActive=true ORDER BY Name,Email");
                    request.Query("roles", "SELECT Id,Name,ParentRoleId FROM UserRole ORDER BY ParentRoleId,Name");
                    var result = await client.Composite.PostAsync(request);

                    var users = new JArray(client.GetEnumerable(result.Queries("users")));
                    var userProfiles = new JArray(users
                        .Select(user => (string)user.SelectToken("Profile.Name") ?? "")
                        .Where(r => !string.IsNullOrEmpty(r))
                        .Distinct().OrderBy(r => r));
                    var userRoles = new JObject
                    {
                        ["id"] = client.InstanceUrl,
                        ["name"] = Resource.GetOrgLabel(client.InstanceUrl),
                        ["url"] = "",
                        ["users"] = new JArray(),
                        ["children"] = GetOrgData(client.GetEnumerable(result.Queries("roles")).ToList(), "", users, client.InstanceUrl),
                    };

                    if ((string)State["currentInstanceUrl"] != client.InstanceUrl)
                        throw new InvalidOperationException();

                    Commit(new JObject
                    {
                        ["userProfiles"] = userProfiles,
                        ["userRoles"] = userRoles,
                        ["users"] = users,
                    });
                }),
                Observable.FromAsync(() => client.GetObjectsAsync())
                .SelectMany(obj => Observable.Defer(() => Observable.Start(() =>
                {
                    Commit(new JObject
                    {
                        ["objects"] = new JArray(obj.SObjects),
                    });
                }))),
                Observable.FromAsync(() => client.Tooling.GetObjectsAsync<JObject>())
                .SelectMany(obj => Observable.Defer(() => Observable.Start(() =>
                {
                    Commit(new JObject
                    {
                        ["toolingObjects"] = new JArray(obj.SObjects),
                    });
                })))
            )
            .LastOrDefaultAsync().ToTask().GoOn();
        }

        public string GetPageContent()
        {
            //return "<html>test<img src='/assets/images/avatar1.jpg'/><img src='/assets/images/avatar2.jpg'/></html>";
            var content = string.Join("", Resource.CONTENT_HTML_START, JsonConvert.SerializeObject(State), Resource.CONTENT_HTML_END);
            return content;
        }

        public async Task SetOrganizationAsync(
            string accessToken,
            string instanceUrl,
            string id,
            string refreshToken)
        {
            await Observable.FromAsync(() => OrgSettings.SaveAysnc(json =>
            {
                var settingForSave = new JObject
                {
                    [OAuth.ACCESS_TOKEN] = accessToken,
                    [OAuth.INSTANCE_URL] = instanceUrl,
                    [OAuth.ID] = id,
                    [OAuth.REFRESH_TOKEN] = refreshToken ?? "",
                };
                json[instanceUrl] = settingForSave;
            }))
            .Retry(3);
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
            var oldFile = new FileInfo(Path.Combine(oldDirectoryPath, AppConstants.JSON_ORG_SETTINGS));

            if (newFilePath != oldFile.FullName)
            {
                try
                {
                    var orgData = OrgSettings.Get(d => d);
                    Directory.CreateDirectory(newDirectoryPath);

                    OrgSettings.SetPath(newFilePath);
                    await OrgSettings.SaveAysnc(_old => orgData).GoOn();

                    try { oldFile.Delete(); } catch { }
                    await AppSettings.SaveAysnc(o => o[AppConstants.PATH_ORG_SETTINGS] = newDirectoryPath).GoOn();
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
