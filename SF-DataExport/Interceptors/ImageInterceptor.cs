using PuppeteerSharp;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Interceptors
{
    public class ImageInterceptor : InterceptorBase
    {
        ResourceManager Resource { get; }
        AppStore Store { get; }

        public ImageInterceptor(ResourceManager resource, AppStore store)
        {
            Resource = resource;
            Store = store;
        }

        public override async Task<bool> RequestAsync(Page appPage, Request request)
        {
            if (Resource.IsLoginUrl(request.Url) && request.Url.Contains(".salesforce.com/assets/images/"))
            {
                var imgPath = "images/" + request.Url.Split(".salesforce.com/assets/images/", 2).Last().Split('?').First();
                var img = Resource.GetResourceBytes(imgPath);
                if (img?.LongLength > 0)
                    await InterceptAsync(appPage, request, req => req.RespondAsync(new ResponseData
                    {
                        Status = HttpStatusCode.OK,
                        ContentType = Resource.GetContentType(imgPath),
                        BodyData = img
                    })).GoOn();
                else
                    await InterceptAsync(appPage, request, req => req.ContinueAsync()).GoOn();

                return true;
            }
            return false;
        }
    }
}
