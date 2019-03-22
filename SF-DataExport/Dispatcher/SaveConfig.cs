using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Unit = System.Reactive.Unit;

namespace SF_DataExport.Dispatcher
{
    public class SaveConfig : IDispatcher
    {
        AppStateManager AppState { get; }
        AppSettingsConfig AppSettings { get; }

        public SaveConfig(AppStateManager appState, AppSettingsConfig appSettings)
        {
            AppState = appState;
            AppSettings = appSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            AppState.Commit(new JObject { ["isLoading"] = true });
            var config = payload as JObject;
            var chromePath = (string)config?[AppConstants.PATH_CHROME];
            var orgSettingsPath = (string)config?[AppConstants.PATH_ORG_SETTINGS];
            var newchromePath = (string)config?[AppConstants.PATH_CHROME];

            await Observable.Merge(
                Observable.FromAsync(() => AppState.SaveOrgSettingsPathAsync(orgSettingsPath))
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
                    AppState.Commit(new JObject { ["alertMessage"] = "Save successfully." });
                }
                else
                {
                    AppState.Commit(new JObject { ["alertMessage"] = message });
                }
                return Observable.Empty<Unit>();
            }))
            .Concat()
            .Finally(() => AppState.Commit(new JObject { ["isLoading"] = false }))
            .LastOrDefaultAsync()
            .SubscribeOn(TaskPoolScheduler.Default);
            return null;
        }
    }
}