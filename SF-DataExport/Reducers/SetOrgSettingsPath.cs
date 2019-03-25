using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class SetOrgSettingsPath : IDispatcher
    {
        AppStore Store { get; }

        public SetOrgSettingsPath(AppStore store)
        {
            Store = store;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            try
            {
                Store.Commit(new JObject { ["isLoading"] = true });

                var orgSettingsPath = (string)payload ?? "";
                var errorMessage = await Store.SaveOrgSettingsPathAsync(orgSettingsPath).GoOn();
                if (errorMessage == null)
                {
                    Store.Commit(new JObject { ["alertMessage"] = "Save successfully." });
                }
                else
                {
                    Store.Commit(new JObject { ["alertMessage"] = "No change." });
                }
            }
            finally
            {
                Store.Commit(new JObject { ["isLoading"] = false });
            }
            return null;
        }
    }
}