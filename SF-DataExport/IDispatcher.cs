using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace SF_DataExport
{
    public interface IDispatcher
    {
        Task<JToken> DispatchAsync(JToken payload);
    }
}
