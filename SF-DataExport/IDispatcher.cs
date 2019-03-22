using PuppeteerSharp;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace SF_DataExport
{
    public interface IDispatcher
    {
        Task<JToken> DispatchAsync(JToken payload);
    }
}
