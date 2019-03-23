using PuppeteerSharp;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Interceptor
{
    public class FontInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStateManager AppState { get; }

        public FontInterceptor(ResourceManager resource, AppStateManager appState)
        {
            Resource = resource;
            AppState = appState;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (Resource.IsLoginUrl(request.Url) && request.Url.Contains(".salesforce.com/res/fonts/"))
            {
                var fntPath = "fonts/" + request.Url.Split(".salesforce.com/res/fonts/", 2).Last().Split('?').First();
                var fnt = Resource.GetResourceBytes(fntPath);
                if (fnt?.LongLength > 0)
                    await AppState.InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                    {
                        Status = HttpStatusCode.OK,
                        ContentType = Resource.GetContentType(fntPath),
                        BodyData = fnt
                    })).GoOn();
                else
                    await AppState.InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();

                return true;
            }
            return false;
        }
    }
}
