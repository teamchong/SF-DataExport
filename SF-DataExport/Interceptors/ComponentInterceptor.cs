using PuppeteerSharp;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport.Interceptors
{
    public class ComponentInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStore Store {  get; }

        public ComponentInterceptor(ResourceManager resource, AppStore store)
        {
            Resource = resource;
            Store = store;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (Resource.IsLoginUrl(request.Url) && request.Url.Contains(".salesforce.com/components/"))
            {
                var componentPath = "components/" + request.Url.Split(".salesforce.com/components/", 2).Last().Split('?').First();
                var component = Resource.GetResource(componentPath);
                if (component?.Length > 0)
                {
                    var componentTpl = Resource.GetResource(componentPath.Remove(componentPath.Length - Path.GetExtension(componentPath).Length) + ".tpl");
                    if (componentTpl?.Length > 0)
                    {
                        component = string.Join("", "(function(template){", component, "})(`", HttpUtility.JavaScriptStringEncode(componentTpl), "`)");
                    }
                    else
                    {
                        component = string.Join("", "(function(){", component, "})()");
                    }
                    await InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                    {
                        Status = HttpStatusCode.OK,
                        ContentType = Resource.GetContentType(componentPath),
                        Body = component
                    })).GoOn();
                }
                else
                    await InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();

                return true;
            }
            return false;
        }
    }
}
