using DotNetForce;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport.Interceptor
{
    public class HomeInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStateManager AppState  { get; }

        public HomeInterceptor(ResourceManager resource, AppStateManager appState)
        {
            Resource = resource;
            AppState = appState;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            switch (request.Url)
            {
                case OAuth.REDIRECT_URI:
                case OAuth.REDIRECT_URI_SANDBOX:
                    await AppState.IntercepObservable(appPage, request, () => request.RespondAsync(new ResponseData
                    {
                        Status = HttpStatusCode.Created,
                        ContentType = "text/html",
                        Body = AppState.GetPageContent(),
                        Headers = new Dictionary<string, object> { ["Cache-Control"] = "no-store" },
                    }));
                    return true;
            }
            return false;
        }

        public override async Task<bool> RequestFinishedAsync(Page appPage, Request request)
        {
            if (appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "#") || appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#"))
            {
                await Observable.FromAsync(async () =>
                {
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
                                await AppState.SetOrganizationAsync(
                                    client.AccessToken,
                                    client.InstanceUrl,
                                    client.Id,
                                    client.RefreshToken
                                ).GoOn();
                                AppState.Commit(AppState.GetOrgSettings());
                                AppState.SetCurrentInstanceUrl(client);
                                Resource.ResetCookie();
                                await Resource.GetCookieAsync(client.InstanceUrl, client.AccessToken).GoOn();
                            }
                            catch (Exception ex)
                            {
                                AppState.Commit(new JObject
                                {
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
                            AppState.Commit(new JObject
                            {
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
                            AppState.Commit(new JObject
                            {
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
                        AppState.Commit(new JObject
                        {
                            ["currentAccessToken"] = "",
                            ["currentId"] = "",
                            ["currentInstanceUrl"] = "",
                            ["showLimitsModal"] = false,
                            ["showOrgModal"] = true,
                            ["showPhotosModal"] = false,
                        });
                    }
                    return Resource.GetRedirectUrlByLoginUrl(request.Url);
                })
                .SelectMany(redirectUrl =>
                    Observable.Defer(() => Observable.Start(() =>
                    {
                        AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = redirectUrl });
                    }))
                ).Catch((Exception ex) => Observable.Return(Unit.Default))
                .SubscribeOn(TaskPoolScheduler.Default);
                return true;
            }
            else if (appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "?") || appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"))
            {
                AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                return true;
            }
            return false;
        }

        public override Task<bool> RequestFailedAsync(Page appPage, Request request)
        {
            if (appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "#")
                || appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "#")
                || appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI + "?")
                || appPage.Target.Url.StartsWith(OAuth.REDIRECT_URI_SANDBOX + "?"))
            {
                AppState.Commit(new JObject { [AppConstants.ACTION_REDIRECT] = OAuth.REDIRECT_URI });
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
