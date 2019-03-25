using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Unit = System.Reactive.Unit;

namespace SF_DataExport.Reducers
{
    public class SaveConfig : IDispatcher
    {
        AppStore Store { get; }
        AppSettingsConfig AppSettings { get; }

        public SaveConfig(AppStore store, AppSettingsConfig appSettings)
        {
            Store = store;
            AppSettings = appSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            Store.Commit(new JObject { ["isLoading"] = true });
            var config = payload as JObject;
            var chromePath = (string)config?[AppConstants.PATH_CHROME];
            var orgSettingsPath = (string)config?[AppConstants.PATH_ORG_SETTINGS];
            var newchromePath = (string)config?[AppConstants.PATH_CHROME];

            await Observable.Merge(
                Observable.FromAsync(() => Store.SaveOrgSettingsPathAsync(orgSettingsPath))
                .Catch((Exception ex) => Observable.Return(ex.ToString())),

                Observable.FromAsync(() => AppSettings.SaveAysnc(o => o[AppConstants.PATH_CHROME] = chromePath)).Select(_ => (string)null)
                .Catch((Exception ex) => Observable.Return(ex.ToString()))
            )
            .Scan(new List<string>(), (errorMessages, errorMessage) =>
            {
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    errorMessages.Add(errorMessage);
                }
                return errorMessages;
            })
            .Select(errorMessages => Observable.Defer(() =>
            {
                var message = string.Join(Environment.NewLine, errorMessages);
                if (errorMessages.Count <= 0)
                {
                    Store.Commit(new JObject { ["alertMessage"] = "Save successfully." });
                }
                else
                {
                    Store.Commit(new JObject { ["alertMessage"] = message });
                }
                return Observable.Empty<Unit>();
            }))
            .Concat()
            .Finally(() => Store.Commit(new JObject { ["isLoading"] = false }))
            .LastOrDefaultAsync().ToTask().GoOn();
            return null;
        }
    }
}