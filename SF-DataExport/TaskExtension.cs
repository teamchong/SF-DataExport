using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

namespace SF_DataExport
{
    public static class TaskExtension
    {
        public static ConfiguredTaskAwaitable Continue(this Task task) => task.ConfigureAwait(false);

        public static ConfiguredTaskAwaitable<TResult> Continue<TResult>(this Task<TResult> task) => task.ConfigureAwait(false);

        public static IDisposable SubscribeTask<T>(this IObservable<T> observable) => observable.SubscribeOn(TaskPoolScheduler.Default).Subscribe();
    }
}
