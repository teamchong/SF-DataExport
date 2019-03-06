using DotNetForce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                ["cmdExport"] = "dotnet " + AppDomain.CurrentDomain.FriendlyName + ".dll download@",
                ["currentInstanceUrl"] = "",
                ["isLoading"] = false,
                ["showOrgModal"] = true,
                ["showUserPopover"] = true,
                ["orgSettingsPath"] = OrgSettings.GetDirectoryPath(),
                ["tab"] = "downloaddataexport",
                ["userDisplayName"] = "",
                ["userEmail"] = "",
                ["userId"] = "",
                ["userName"] = "",
                ["userPhoto"] = "",
                ["userExportPath"] = AppDomain.CurrentDomain.BaseDirectory,
                ["userExportSelection"] = "",
                ["userPopoverSelection"] = "",
                ["users"] = new JArray(),
            };
            State.Merge(GetOrgSettings(), MergeSettings);
            Subscribe();
        }

        public void PageAlert(string message)
        {
            Rx.FromAsync(() => AppPage.EvaluateFunctionAsync("alert", message ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        public void PageConfirm(string message)
        {
            Rx.FromAsync(() => AppPage.EvaluateFunctionAsync("confirm", message ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        public void PagePrompt(string message)
        {
            PagePrompt(message, "");
        }

        public void PagePrompt(string message, string defaultValue)
        {
            Rx.FromAsync(() => AppPage.EvaluateFunctionAsync("prompt", message ?? "", defaultValue ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        public void SubscribeDispatch(JArray actions)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i] as JObject;
                if (action != null)
                {
                    var type = action["type"]?.ToString();

                    switch (type)
                    {
                        case "loginAsUser":
                            Rx.Start(() =>
                            {
                                var payload = action["payload"] as JObject;
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var id = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                                var url = Resource.GetLoginUrlAs(instanceUrl, id, userId, "/");
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, url);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.GetString(AppConstants.CHROME_PATH));
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "viewPage":
                            Rx.Start(() =>
                            {
                                var payload = action["payload"] as JObject;
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var url = payload["url"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, url);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.GetString(AppConstants.CHROME_PATH));
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "viewUserPage":
                            Rx.Start(() =>
                            {
                                var payload = action["payload"] as JObject;
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var url = instanceUrl + "/" + userId + "?noredirect=1";
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, url);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.GetString(AppConstants.CHROME_PATH));
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "downloadExports":
                            Rx.FromAsync(async () =>
                            {
                                var payload = action["payload"] as JObject;
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var exportPath = payload["exportPath"]?.ToString() ?? "";
                                var id = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var url = string.IsNullOrEmpty(userId) ?
                                    instanceUrl + "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport" :
                                    Resource.GetLoginUrlAs(instanceUrl, id, userId, "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport");
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, url);
                                await AppPage.GoToAsync(urlWithAccessCode);
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "removeOrg":
                            Rx.FromAsync(async () =>
                            {
                                var instanceUrl = action["payload"]?.ToString() ?? "";
                                var loginUrl = OrgSettings.Get(o => o[instanceUrl][OAuth.LOGIN_URL])?.ToString() ?? "";
                                await OrgSettings.SaveAysnc(json =>
                                {
                                    if (json[instanceUrl] != null)
                                    {
                                        json.Remove(instanceUrl);
                                    }
                                });
                                Commit(GetOrgSettings());
                                if (State["currentInstanceUrl"]?.ToString() == instanceUrl)
                                {
                                    Commit(new JObject
                                    {
                                        ["currentInstanceUrl"] = "",
                                        ["showOrgModal"] = true,
                                        ["userDisplayName"] = "",
                                        ["userEmail"] = "",
                                        ["userId"] = "",
                                        ["userName"] = "",
                                        ["userPhoto"] = "",
                                        ["userPopoverSelection"] = "",
                                        ["users"] = new JArray()
                                    });
                                }
                                var oauthPage = instanceUrl +
                                    "/identity/app/connectedAppsUserList.apexp?app_name=SFDataExport&consumer_key=" +
                                    HttpUtility.UrlEncode(Resource.GetClientIdByLoginUrl(loginUrl));
                                Resource.OpenBrowserIncognito(oauthPage, AppSettings.GetString(AppConstants.CHROME_PATH));
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "removeOfflineAccess":
                            Rx.FromAsync(async () =>
                            {
                                var instanceUrl = action["payload"]?.ToString() ?? "";
                                await OrgSettings.SaveAysnc(json =>
                                {
                                    if (json[instanceUrl] != null)
                                    {
                                        json[instanceUrl][OAuth.REFRESH_TOKEN] = "";
                                    }
                                });
                                Commit(new JObject
                                {
                                    ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                                        .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN])?.ToString())))
                                });
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "saveConfig":
                            Rx.Start(() =>
                            {
                                var config = action["payload"] as JObject;
                                var chromePath = config?[AppConstants.CHROME_PATH]?.ToString();
                                var orgSettingsPath = config?[AppConstants.ORG_SETTINGS_PATH]?.ToString();
                                var newchromePath = config?[AppConstants.CHROME_PATH]?.ToString();

                                Rx.Merge(
                                    Rx.FromAsync(() => SaveOrgSettingsPathAsync(orgSettingsPath))
                                    .Catch((Exception ex) => Rx.Return(ex.ToString())),
                                    Rx.FromAsync(() => AppSettings.SaveAysnc(o => o[AppConstants.CHROME_PATH] = chromePath))
                                    .Select(_ => (string)null)
                                    .Catch((Exception ex) => Rx.Return(ex.ToString()))
                                )
                                .Scan(new List<string>(), (errorMessages, errorMessage) =>
                                {
                                    if (!string.IsNullOrEmpty(errorMessage))
                                    {
                                        errorMessages.Add(errorMessage);
                                    }
                                    return errorMessages;
                                })
                                .Select(errorMessages => Rx.Start(() =>
                                {
                                    var message = string.Join(Environment.NewLine, errorMessages);
                                    if (errorMessages.Count <= 0)
                                    {
                                        PageAlert("Save successfully.");
                                    }
                                    else
                                    {
                                        PageAlert(message);
                                    }
                                }))
                                .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "setOrgSettingsPath":
                            Rx.FromAsync(async () =>
                            {
                                var orgSettingsPath = action["payload"]?.ToString() ?? "";
                                var errorMessage = await SaveOrgSettingsPathAsync(orgSettingsPath);
                                if (errorMessage == null)
                                {
                                    PageAlert("Save successfully.");
                                }
                                else
                                {
                                    PageAlert("No change.");
                                }
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "attemptLogin":
                            Rx.FromAsync(async () =>
                            {
                                var attemptingDomain = Regex.Replace(action["payload"]?.ToString() ?? "", "^https?://", "");

                                if (!string.IsNullOrEmpty(attemptingDomain))
                                {
                                    var loginUrl = "https://" + attemptingDomain;

                                    if (attemptingDomain != "login.salesforce.com" && attemptingDomain != "test.salesforce.com")
                                    {
                                        var instanceUrl = "https://" + attemptingDomain;
                                        var savedOrg = OrgSettings.Get(o => o[instanceUrl]);
                                        if (savedOrg != null)
                                        {
                                            var accessToken = savedOrg[OAuth.ACCESS_TOKEN]?.ToString() ?? "";
                                            var refreshToken = savedOrg[OAuth.REFRESH_TOKEN]?.ToString() ?? "";
                                            loginUrl = savedOrg[OAuth.LOGIN_URL]?.ToString() ?? "";
                                            if (!Uri.IsWellFormedUriString(loginUrl, UriKind.Absolute)) loginUrl = "https://login.salesforce.com";
                                            var client = new DNFClient(instanceUrl, accessToken, refreshToken);

                                            try
                                            {
                                                await client.TokenRefreshAsync(new Uri(loginUrl), Resource.GetClientIdByLoginUrl(loginUrl));
                                                await SetOrganizationAsync(
                                                    client.AccessToken,
                                                    client.InstanceUrl,
                                                    loginUrl,
                                                    client.Id,
                                                    client.RefreshToken);
                                                SetCurrentInstanceUrl(client);
                                                return;
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine(ex.ToString());
                                                PageAlert(ex.Message);
                                            }
                                        }
                                    }

                                    var url = loginUrl + "/services/oauth2/authorize" +
                                        "?response_type=token" +
                                        "&client_id=" + HttpUtility.UrlEncode(Resource.GetClientIdByLoginUrl(loginUrl)) +
                                        "&redirect_uri=" + HttpUtility.UrlEncode(Resource.GetRedirectUrlByLoginUrl(loginUrl)) +
                                        "&state=" + HttpUtility.UrlEncode(loginUrl) +
                                        "&display=popup";
                                    await AppPage.GoToAsync(url);
                                }
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        default:
                            Commit(new JObject { [type] = action["payload"] });
                            break;
                    }
                }
            }
        }

        public void Subscribe()
        {
            CommitSubject.Buffer(TimeSpan.FromMilliseconds(100)).Where(newStates => newStates.Any())
                .Select(newStates =>
                    Rx.FromAsync(() => AppPage.EvaluateFunctionAsync("storeCommit", new JArray(newStates)))
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
                    .Where(org => Resource.IsSandboxLoginUrl(OrgSettings.Get(o => o[org]?[OAuth.LOGIN_URL])?.ToString())));
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
                State.Merge(newState, MergeSettings);
                CommitSubject.OnNext(newState);
            }
        }

        public void SetCurrentInstanceUrl(DNFClient client)
        {
            Commit(new JObject
            {
                ["currentInstanceUrl"] = client.InstanceUrl,
                ["showOrgModal"] = false,
                ["userDisplayName"] = "",
                ["userEmail"] = "",
                ["userId"] = "",
                ["userName"] = "",
                ["userPhoto"] = "",
                ["userPopoverSelection"] = "",
                ["users"] = new JArray()
            });
            Rx.Merge(

                Rx.FromAsync(() => client.GetEnumerableAsync("SELECT Id,Name,Email FROM User WHERE IsActive=true"))
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
                        ["userPopoverSelection"] = "",
                    }))
                    .Catch(Rx.Empty<Unit>()),

                    Rx.FromAsync(() => Resource.GetDataViaAccessToken(client.InstanceUrl, client.AccessToken,
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
            .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        public string GetPageContent()
        {
            var content = string.Join("", Resource.CONTENT_HTML_START, JsonConvert.SerializeObject(State), Resource.CONTENT_HTML_END);
            return content;
        }

        public async Task SetOrganizationAsync(
            string accessToken,
            string instanceUrl,
            string loginUrl,
            string id,
            string refreshToken)
        {
            await OrgSettings.SaveAysnc(json =>
            {
                var settingForSave = new JObject
                {
                    [OAuth.ACCESS_TOKEN] = accessToken,
                    [OAuth.INSTANCE_URL] = instanceUrl,
                    [OAuth.LOGIN_URL] = loginUrl,
                    [OAuth.ID] = id,
                    [OAuth.REFRESH_TOKEN] = refreshToken ?? "",
                };
                json[instanceUrl] = settingForSave;
            });
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
                    await File.WriteAllTextAsync(newFilePath, orgData.ToString());
                    try { File.Delete(oldFilePath); } catch { }
                    await AppSettings.SaveAysnc(o => o[AppConstants.ORG_SETTINGS_PATH] = newDirectoryPath);
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
