using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;

namespace SF_DataExport
{
    public static class IObservableExtension
    {
        public static IDisposable ScheduleTask<TResult>(this IObservable<TResult> observable) => 
            observable.ObserveOn(TaskPoolScheduler.Default).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
    }
}
