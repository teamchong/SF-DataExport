using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace SF_DataExport
{
    public abstract class InterceptorBase
    {
        public virtual Task<bool> RequestAsync(Page appPage, Request request) => Task.FromResult(false);

        public virtual Task<bool> RequestFinishedAsync(Page appPage, Request request) => Task.FromResult(false);

        public virtual Task<bool> RequestFailedAsync(Page appPage, Request request) => Task.FromResult(false);

        public virtual Task<bool> ResponseAsync(Page appPage, Response request) => Task.FromResult(false);

        public virtual Task<bool> DOMContentLoadedAsync(Page appPage) => Task.FromResult(false);
        
        public virtual Task<bool> ErrorAsync(Page appPage, string errorMessage) => Task.FromResult(false);
        
        public virtual Task<bool> PageErrorAsync(Page appPage, string errorMessage) => Task.FromResult(false);
    }
}
