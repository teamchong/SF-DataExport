using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class RemoveOfflineAccess : IDispatcher
    {
        AppStore Store { get; }
        JsonConfig OrgSettings { get; }

        public RemoveOfflineAccess(AppStore store, JsonConfig orgSettings)
        {
            Store = store;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)payload ?? "";
            await OrgSettings.SaveAysnc(json =>
            {
                if (json[instanceUrl] != null)
                {
                    json[instanceUrl][OAuth.REFRESH_TOKEN] = "";
                }
            }).GoOn();
            Store.Commit(new JObject
            {
                ["orgOfflineAccess"] = new JArray(OrgSettings.List()
                .Where(org => !string.IsNullOrEmpty((string)OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN]))))
            });
            return null;
        }
    }
}