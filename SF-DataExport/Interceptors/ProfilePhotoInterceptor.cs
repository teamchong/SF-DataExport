using PuppeteerSharp;
using System;
using System.Net;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace SF_DataExport.Interceptors
{
    public class ProfilePhotoInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        JsonConfig OrgSettings { get; }
        AppStore Store { get; }

        public ProfilePhotoInterceptor(ResourceManager resource, OrgSettingsConfig orgSettings, AppStore store)
        {
            Resource = resource;
            OrgSettings = orgSettings;
            Store = store;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (request.Url.Contains(".content.force.com/profilephoto/"))
            {
                await Observable.FromAsync(async () =>
                {
                    var instanceUrl = (string)Store.GetState("currentInstanceUrl");
                    if (string.IsNullOrEmpty(instanceUrl))
                        await InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();
                    else
                    {
                        var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]);
                        var bytes = await Resource.GetBytesViaAccessTokenAsync(instanceUrl, accessToken, request.Url);
                        if (bytes?.LongLength > 0)
                            await InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                            {
                                Status = HttpStatusCode.OK,
                                ContentType = "image/png",
                                BodyData = bytes
                            })).GoOn();
                        else
                            await InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();
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
