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
            if (Resource.IsLoginUrl(request.Url) && request.Url.Contains(".salesforce.com/fonts/"))
            {
                var fntPath = "fonts/" + request.Url.Split(".salesforce.com/fonts/", 2).Last().Split('?').First();
                var fnt = Resource.GetResourceBytes(fntPath);
                if (fnt != null)
                    await AppState.IntercepObservable(appPage, request, () => request.RespondAsync(new ResponseData
                    {
                        Status = HttpStatusCode.OK,
                        ContentType = Resource.GetContentType(fntPath),
                        BodyData = fnt
                    }));
                else
                    await AppState.IntercepObservable(appPage, request, () => request.ContinueAsync());
                return true;
            }
            return false;
        }
    }
}
