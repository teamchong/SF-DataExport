using PuppeteerSharp;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Interceptors
{
    public class ResInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStore Store { get; }

        public ResInterceptor(ResourceManager resource, AppStore store)
        {
            Resource = resource;
            Store = store;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (Resource.IsLoginUrl(request.Url))
            {
                if (request.Url.EndsWith(".salesforce.com/favicon.ico"))
                {
                    var file = Resource.GetResourceBytes("images/favicon.ico");
                    if (file?.LongLength > 0)
                        await InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType("favicon.ico"),
                            BodyData = file
                        })).GoOn();
                    else
                        await InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();

                    return true;
                }
                else if (request.Url.Contains(".salesforce.com/res/") && (request.Url.EndsWith(".js") || request.Url.EndsWith(".css")))
                {
                    var path = request.Url.Split(".salesforce.com/res/", 2).Last().Split('?').First();
                    var file = Resource.GetResource(path);
                    if (file?.Length > 0)
                        await InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                        {
                            Status = HttpStatusCode.OK,
                            ContentType = Resource.GetContentType(path),
                            Body = file
                        })).GoOn();
                    else
                        await InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();

                    return true;
                }
            }
            return false;
        }
    }
}
