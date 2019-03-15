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
            observable.Catch((Exception ex) =>
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
                return Observable.Empty<TResult>();
            })
            .ObserveOn(TaskPoolScheduler.Default).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
    }
}
