using PuppeteerSharp;
using System.Linq;
using System.Net;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Interceptor
{
    public class ComponentInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStateManager AppState {  get; }

        public ComponentInterceptor(ResourceManager resource, AppStateManager appState)
        {
            Resource = resource;
            AppState = appState;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (Resource.IsLoginUrl(request.Url) && request.Url.Contains(".salesforce.com/components/"))
            {
                var componentPath = "components/" + request.Url.Split(".salesforce.com/components/", 2).Last().Split('?').First();
                var component = Resource.GetResource(componentPath);
                if (!string.IsNullOrEmpty(component))
                {
                    var componentTpl = Resource.GetResource(componentPath.Remove(componentPath.Length - Path.GetExtension(componentPath).Length) + ".tpl");
                    if (!string.IsNullOrEmpty(componentTpl))
                    {
                        component = string.Join("", "(function(template){", component, "})(`", componentTpl, "`)");
                    }
                    else
                    {
                        component = string.Join("", "(function(){", component, "})()");
                    }
                    await AppState.IntercepObservable(appPage, request, () => request.RespondAsync(new ResponseData
                    {
                        Status = HttpStatusCode.OK,
                        ContentType = Resource.GetContentType(componentPath),
                        Body = component
                    }));
                }
                else
                    await AppState.IntercepObservable(appPage, request, () => request.ContinueAsync());
                return true;
            }
            return false;
        }
    }
}
