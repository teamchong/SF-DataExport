using PuppeteerSharp;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Interceptor
{
    public class RootInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStateManager AppState { get; }

        public RootInterceptor(ResourceManager resource, AppStateManager appState)
        {
            Resource = resource;
            AppState = appState;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (Resource.IsLoginUrl(request.Url) && request.Url.Count(c => c == '/') == 3 && !request.Url.EndsWith('/'))
            {
                var path = request.Url.Split('/').LastOrDefault();
                var file = Resource.GetResourceBytes(path);
                if (file != null)
                    await AppState.IntercepObservable(appPage, request, () => request.RespondAsync(new ResponseData
                    {
                        Status = HttpStatusCode.OK,
                        ContentType = Resource.GetContentType(path),
                        BodyData = file
                    }));
                else
                    await AppState.IntercepObservable(appPage, request, () => request.ContinueAsync());
                return true;
            }
            return false;
        }
    }
}
