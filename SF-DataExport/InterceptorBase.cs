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
        public virtual Task<bool> RequestAsync(Page appPage, Request request) => null;

        public virtual Task RequestFinishedAsync(Page appPage, Request request) => null;

        public virtual Task RequestFailedAsync(Page appPage, Request request) => null;

        public virtual Task ResponseAsync(Page appPage, Response request) => null;

        public virtual Task DOMContentLoadedAsync(Page appPage) => null;
        
        public virtual Task ErrorAsync(Page appPage, string errorMessage) => null;
        
        public virtual Task PageErrorAsync(Page appPage, string errorMessage) => null;
    }
}
