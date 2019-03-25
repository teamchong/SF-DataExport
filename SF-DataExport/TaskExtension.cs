using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SF_DataExport
{
    public static class TaskExtension
    {
        public static ConfiguredTaskAwaitable GoOn(this Task task) => task.ConfigureAwait(false);

        public static ConfiguredTaskAwaitable<TResult> GoOn<TResult>(this Task<TResult> task) => task.ConfigureAwait(false);
    }
}
