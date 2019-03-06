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
        Page Page { get; set; }

        private AppDialog(ResourceManager resource, JsonConfig appSettings, JsonConfig orgSettings, AppStateManager appState)
        {
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            AppState = appState;
        }

        public static async Task<AppDialog> CreateAsync(JsonConfig appSettings, JsonConfig orgSettings, ResourceManager resource)
        {
            var chromePath = appSettings.Get(o => o["chromePath"])?.ToString();
            var appState = new AppStateManager(appSettings, orgSettings, resource);
            var appDialog = new AppDialog(resource, appSettings, orgSettings, appState);

            var closeSubject = new Subject<bool>();
            var browser = await Puppeteer.LaunchAsync(resource.GetLaunchOptions(chromePath));
            try
            {
                browser.Closed += (object sender, EventArgs e) => closeSubject.OnCompleted();

                appDialog.Page = await Rx.FromAsync(() => browser.PagesAsync()).Select(p => p.Length > 0 ? p[0] : null)
                    .SelectMany(p => Rx.FromAsync(() => appDialog.SetupPageAsync(p, true)));

                appState.Subscribe(appDialog.Page);

                await appDialog.Page.GoToAsync(OAuth.REDIRECT_URI);

                await closeSubject.Count();
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
            return appDialog;
        }

        public void PageAlert(string message)
        {
            Rx.FromAsync(() => Page.EvaluateFunctionAsync("alert", message ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        public void PageConfirm(string message)
        {
            Rx.FromAsync(() => Page.EvaluateFunctionAsync("confirm", message ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        public void PagePrompt(string message)
        {
            PagePrompt(message, "");
        }

        public void PagePrompt(string message, string defaultValue)
        {
            Rx.FromAsync(() => Page.EvaluateFunctionAsync("prompt", message ?? "", defaultValue ?? ""))
                .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        async Task<Page> SetupPageAsync(Page page, bool interception)
        {
            await Task.WhenAll(
                page.SetRequestInterceptionAsync(interception),
                page.SetCacheEnabledAsync(false),
                page.SetBypassCSPAsync(true),
                page.ExposeFunctionAsync("subscribeDispatch", (JArray actions) =>
                {
                    SubscribeDispatch(actions);
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

        string GetPageContent()
        {
            var content = string.Join("", Resource.CONTENT_HTML_START, JsonConvert.SerializeObject(AppState.Value), Resource.CONTENT_HTML_END);
            return content;
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
                        await Page.SetRequestInterceptionAsync(true);
                        PageInterception(() => e.Request.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.Created,
                            ContentType = "text/html",
                            Body = GetPageContent()
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
            switch (Page.Target.Url)
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
                                    await SetOrganizationAsync(
                                        newOrg[OAuth.ACCESS_TOKEN]?.ToString(),
                                        newOrg[OAuth.INSTANCE_URL]?.ToString(),
                                        newOrg[OAuth.LOGIN_URL]?.ToString(),
                                        newOrg[OAuth.ID]?.ToString(),
                                        newOrg[OAuth.REFRESH_TOKEN]?.ToString());
                                    AppState.Commit(AppState.GetOrgSettings());
                                    SetCurrentInstanceUrl(client);
                                }
                                catch (Exception ex)
                                {
                                    AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                                    Console.WriteLine($"Login fail (REST)\n${ex.Message}");
                                    PageAlert($"Login fail (REST)\n${ex.Message}");
                                }
                            }
                            else if (new[] { "error", "error_description" }.All(s => !string.IsNullOrEmpty(newOrg[s]?.ToString())))
                            {
                                AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                                Console.WriteLine($"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}");
                                PageAlert($"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}");
                            }
                            else
                            {
                                AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                                Console.WriteLine($"Login fail (Unknown)\n${newOrg}");
                                PageAlert($"Login fail (Unknown)\n${newOrg}");
                            }
                        }
                        else
                        {
                            AppState.Commit(new JObject { ["currentInstanceUrl"] = "", ["showOrgModal"] = true });
                        }
                    })
                    .SelectMany(redirectUrl => Rx.FromAsync(() => Page.GoToAsync(OAuth.REDIRECT_URI)))
                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                || url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    Rx.FromAsync(() => Page.GoToAsync(OAuth.REDIRECT_URI))
                        .SelectMany(_ => Rx.FromAsync(() => Page.ReloadAsync()))
                        .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
        }

        void Page_RequestFailed(object sender, RequestEventArgs e)
        {
            switch (Page.Target.Url)
            {
                case var url when url.StartsWith(OAuth.REDIRECT_URI + "#") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                || url.StartsWith(OAuth.REDIRECT_URI + "?") || url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"):
                    Rx.FromAsync(() => Page.GoToAsync(OAuth.REDIRECT_URI))
                        .SelectMany(_ => Rx.FromAsync(() => Page.ReloadAsync()))
                        .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
        }

        void Page_DOMContentLoaded(object sender, EventArgs e)
        {
            switch (Page.Url)
            {
                case var url when url.Contains("/ui/setup/export/DataExportPage/d?setupid=DataManagementExport"):

                    Rx.FromAsync(() => Page.XPathAsync("//tr[contains(@class,'dataRow')]"))
                    .Select(els => els.ToObservable()).Concat()
                    .Select(el => Rx.FromAsync(async () =>
                    {
                        var a = (await el.XPathAsync("//a[1]")).FirstOrDefault();
                        var href = await a.GetPropertiesAsync();
                        var th = (await el.XPathAsync("//th[1]")).FirstOrDefault();
                        var filename = await th.GetPropertiesAsync();
                        return a;
                    })).Concat()
                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                    break;
            }
        }

        async Task SetOrganizationAsync(
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
            AppState.Commit(new JObject { ["orgSettings"] = new JArray(OrgSettings.List()) });
        }

        async Task<string> SaveOrgSettingsPathAsync(string newDirectoryPath)
        {
            if (string.IsNullOrEmpty(newDirectoryPath))
            {
                newDirectoryPath = AppContext.BaseDirectory;
            }
            var newFilePath = Path.Combine(newDirectoryPath, "orgsettings.json");

            var oldDirectoryPath = OrgSettings.GetDirectoryPath();
            var oldFilePath = Path.Combine(newDirectoryPath, "orgsettings.json");

            if (newFilePath != oldFilePath)
            {
                try
                {
                    var orgData = OrgSettings.Read();
                    await File.WriteAllTextAsync(newFilePath, orgData.ToString());
                    try { File.Delete(oldFilePath); } catch { }
                    await AppSettings.SaveAysnc(o => o["orgSettingsPath"] = newDirectoryPath);
                    OrgSettings.SetPath(newFilePath);
                    AppState.Commit(new JObject { ["orgSettingsPath"] = OrgSettings.GetDirectoryPath() });
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

        void SubscribeDispatch(JArray actions)
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
                                Resource.OpenBrowser(urlWithAccessCode);
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "loginAsUserIncognito":
                            Rx.Start(() =>
                            {
                                var payload = action["payload"] as JObject;
                                var instanceUrl = payload["instanceUrl"]?.ToString() ?? "";
                                var userId = payload["userId"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var id = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ID])?.ToString() ?? "";
                                var url = Resource.GetLoginUrlAs(instanceUrl, id, userId, "/");
                                var urlWithAccessCode = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, url);
                                Resource.OpenBrowserIncognito(urlWithAccessCode, AppSettings.Get(o => o["chromePath"])?.ToString());
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
                                Resource.OpenBrowser(urlWithAccessCode);
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
                                Resource.OpenBrowser(urlWithAccessCode);
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "downloadDataExport":
                            Rx.FromAsync(async () =>
                            {
                                var instanceUrl = AppState.Value["currentInstanceUrl"]?.ToString() ?? "";
                                var accessToken = OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN])?.ToString() ?? "";
                                var url = Resource.GetUrlViaAccessToken(instanceUrl, accessToken, "/ui/setup/export/DataExportPage/d?setupid=DataManagementExport");
                                await Page.GoToAsync(url);
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
                                AppState.Commit(AppState.GetOrgSettings());
                                if (AppState.Value["currentInstanceUrl"]?.ToString() == instanceUrl)
                                {
                                    AppState.Commit(new JObject
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
                                Resource.OpenBrowser(oauthPage);
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
                                AppState.Commit(new JObject
                                {
                                    ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                                        .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN])?.ToString())))
                                });
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        case "saveConfig":
                            Rx.FromAsync(async () =>
                            {
                                var config = action["payload"] as JObject;
                                var chromePath = config?["chromePath"]?.ToString();
                                var orgSettingsPath = config?["orgSettingsPath"]?.ToString();
                                var newchromePath = config?["chromePath"]?.ToString();

                                Rx.Merge(
                                    Rx.FromAsync(() => SaveOrgSettingsPathAsync(orgSettingsPath))
                                    .Catch((Exception ex) => Rx.Return(ex.ToString())),
                                    Rx.FromAsync(() => AppSettings.SaveAysnc(o => o["chromePath"] = chromePath))
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
                                    Rx.FromAsync(() => Page.GoToAsync(url))
                                    .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                                }
                            }).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
                            break;
                        default:
                            AppState.Commit(new JObject { [type] = action["payload"] });
                            break;
                    }
                }
            }
        }

        void SetCurrentInstanceUrl(DNFClient client)
        {
            AppState.Commit(new JObject
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
                    () => AppState.Value["currentInstanceUrl"]?.ToString() == client.InstanceUrl,
                    Rx.Start(() => AppState.Commit(new JObject { ["users"] = users })),
                    Rx.Throw<Unit>(new InvalidOperationException())
                ))
                .Catch(Rx.Empty<Unit>()),

                Rx.FromAsync(() => client.UserInfo())
                .SelectMany(userInfo => Rx.Merge(

                    Rx.Start(() => AppState.Commit(new JObject
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
                            () => AppState.Value["currentInstanceUrl"]?.ToString() == client.InstanceUrl,
                            Rx.Start(() => AppState.Commit(new JObject { ["userPhoto"] = userPhoto })),
                            Rx.Throw<Unit>(new InvalidOperationException())
                        )
                    )
                ))

            )
            .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

        public void PageInterception(Func<Task> func, Request request)
        {
            Rx.Concat(
                Rx.If(() => request.Method != HttpMethod.Get,
                Rx.FromAsync(() => Page.SetRequestInterceptionAsync(false))),
                Rx.FromAsync(func)
            )
            .Catch((Exception ex) => Rx.Empty<Unit>())
            .SubscribeOn(TaskPoolScheduler.Default).Subscribe();
        }

    }
}
