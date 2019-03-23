using PuppeteerSharp;
using System.Linq;
using System;
using System.Net;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace SF_DataExport.Interceptor
{
    public class ProfilePhotoInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        JsonConfig OrgSettings { get; }
        AppStateManager AppState { get; }

        public ProfilePhotoInterceptor(ResourceManager resource, OrgSettingsConfig orgSettings, AppStateManager appState)
        {
            Resource = resource;
            OrgSettings = orgSettings;
            AppState = appState;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (request.Url.Contains(".content.force.com/profilephoto/"))
            {
                await Observable.FromAsync(async () =>
                {
                    var instanceUrl = (string)AppState.GetState("currentInstanceUrl");
                    if (string.IsNullOrEmpty(instanceUrl))
                        await AppState.InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();
                    else
                    {
                        var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]);
                        var bytes = await Resource.GetBytesViaAccessTokenAsync(instanceUrl, accessToken, request.Url);
                        if (bytes?.LongLength > 0)
                            await AppState.InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                            {
                                Status = HttpStatusCode.OK,
                                ContentType = "image/png",
                                BodyData = bytes
                            })).GoOn();
                        else
                            await AppState.InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();
                    }
                })
                .Retry(1)
                .Catch((Exception _) => Observable.Return(Unit.Default))
                .LastOrDefaultAsync().ToTask().GoOn();
                return true;
            }
            return false;
        }
    }
}
