using DotNetForce;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport.Interceptors
{
    public class AppPageInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStore Store { get; }

        public AppPageInterceptor(ResourceManager resource, AppStore store)
        {
            Resource = resource;
            Store = store;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (Resource.IsAppPage(request.Url))
            {
                Store.Commit(new JObject { ["isLoading"] = true });
                await InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                {
                    Status = HttpStatusCode.Created,
                    ContentType = "text/html",
                    Body = Store.GetPageContent(),
                    Headers = new Dictionary<string, object> { ["Cache-Control"] = "no-store" },
                })).GoOn();
                return true;
            }
            return false;
        }

        public override async Task RequestFinishedAsync(Page appPage, Request request)
        {
            if (Resource.IsAppPage(request.Url))
            {
                if (appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "#") || appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#"))
                {
                    await Observable.FromAsync(async () =>
                    {
                        var redirectUrl = Resource.GetRedirectUrlByLoginUrl(request.Url);
                        var tokens = HttpUtility.ParseQueryString(appPage.Target.Url.Split(new[] { '#' }, 2).Last());

                        if (tokens.AllKeys.Where(k => k != null).Any())
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
                                    await Store.SetOrganizationAsync(
                                        client.AccessToken,
                                        client.InstanceUrl,
                                        client.Id,
                                        client.RefreshToken
                                    ).GoOn();
                                    var newState = Store.GetOrgSettings();
                                    newState[AppConstants.ACTION_REDIRECT] = redirectUrl;
                                    await Store.SetCurrentInstanceUrlAync(client, newState).GoOn();
                                    await Resource.GetCookieAsync(client.InstanceUrl, client.AccessToken).GoOn();
                                }
                                catch (Exception ex)
                                {
                                    Store.Commit(new JObject
                                    {
                                        [AppConstants.ACTION_REDIRECT] = redirectUrl,
                                        ["alertMessage"] = $"Login fail (REST)\n${ex.Message}",
                                        ["currentAccessToken"] = "",
                                        ["currentId"] = "",
                                        ["currentInstanceUrl"] = "",
                                        ["showLimitsModal"] = false,
                                        ["showOrgModal"] = true,
                                        ["showPhotosModal"] = false,
                                    });
                                    Console.WriteLine($"Login fail (REST)\n${ex.Message}");
                                }
                            }
                            else if (new[] { "error", "error_description" }.All(s => !string.IsNullOrEmpty((string)newOrg[s])))
                            {
                                Store.Commit(new JObject
                                {
                                    [AppConstants.ACTION_REDIRECT] = redirectUrl,
                                    ["alertMessage"] = $"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}",
                                    ["currentAccessToken"] = "",
                                    ["currentId"] = "",
                                    ["currentInstanceUrl"] = "",
                                    ["showLimitsModal"] = false,
                                    ["showOrgModal"] = true,
                                    ["showPhotosModal"] = false,
                                });
                                Console.WriteLine($"Login fail ({newOrg["error"]})\n${newOrg["error_description"]}");
                            }
                            else
                            {
                                Store.Commit(new JObject
                                {
                                    [AppConstants.ACTION_REDIRECT] = redirectUrl,
                                    ["alertMessage"] = $"Login fail (Unknown)\n${newOrg}",
                                    ["currentAccessToken"] = "",
                                    ["currentId"] = "",
                                    ["currentInstanceUrl"] = "",
                                    ["showLimitsModal"] = false,
                                    ["showOrgModal"] = true,
                                    ["showPhotosModal"] = false,
                                });
                                Console.WriteLine($"Login fail (Unknown)\n${newOrg}");
                            }
                        }
                        else
                        {
                            Store.Commit(new JObject
                            {
                                [AppConstants.ACTION_REDIRECT] = redirectUrl,
                                ["currentAccessToken"] = "",
                                ["currentId"] = "",
                                ["currentInstanceUrl"] = "",
                                ["showLimitsModal"] = false,
                                ["showOrgModal"] = true,
                                ["showPhotosModal"] = false,
                            });
                        }
                    })
                    .Catch((Exception ex) => Observable.Return(Unit.Default))
                    .LastOrDefaultAsync().ToTask().GoOn();
                }
                else if (appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "?") || appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"))
                {
                    Store.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                }
                else
                {
                    Store.Commit(new JObject { ["isLoading"] = false });
                }
            }
        }

        public override Task RequestFailedAsync(Page appPage, Request request)
        {
            if (Resource.IsAppPage(request.Url) && (
                appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "#") ||
                appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#") ||
                appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "?") ||
                appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?")))
            {
                Store.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
            }
            return null;
        }

        public override Task DOMContentLoadedAsync(Page appPage)
        {
            if (Resource.IsAppPage(appPage.Url))
            {
                var state = new JObject();
                Store.ListState().ForEach(stateName => state[stateName] = Store.GetState(stateName));
                Store.Commit(state);
            }
            return null;
        }
    }
}
