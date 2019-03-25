using PuppeteerSharp;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace SF_DataExport
{
    public abstract class InterceptorBase
    {
        public virtual Task<bool> RequestAsync(Page appPage, Request request) => null;

        public virtual Task RequestFinishedAsync(Page appPage, Request request) => null;

        public virtual Task RequestFailedAsync(Page appPage, Request request) => null;

        public virtual Task ResponseAsync(Page appPage, Response request) => null;

        public virtual Task DOMContentLoadedAsync(Page appPage) => null;
        
        public virtual Task ErrorAsync(Page appPage, string errorMessage) => null;
        
        public virtual Task PageErrorAsync(Page appPage, string errorMessage) => null;
        

        public Task<Unit> InterceptAsync(Page appPage, Request request, Func<Request, Task> funcAsync)
        {
            return Observable.FromAsync(() => funcAsync(request))
            .Catch((Exception ex) => Observable.FromAsync(async () =>
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
                await request.AbortAsync();
            }))
            .LastOrDefaultAsync()
            .ToTask();
        }
    }
}
