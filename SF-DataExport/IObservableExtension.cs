using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace SF_DataExport
{
    public static class IObservableExtension
    {
//        public static IDisposable ScheduleTask<TResult>(this IObservable<TResult> observable)
//        {
//            return observable
//               .SuppressErrors()
//               .ObserveOn(TaskPoolScheduler.Default).SubscribeOn(TaskPoolScheduler.Default).Subscribe();
//        }

        public static IObservable<TResult> SuppressErrors<TResult>(this IObservable<TResult> observable)
        {
            return observable.Catch((Exception ex) =>
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
                return Observable.Empty<TResult>();
            });
        }
    }
}
