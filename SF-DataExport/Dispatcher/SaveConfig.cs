using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Unit = System.Reactive.Unit;

namespace SF_DataExport.Dispatcher
{
    public class SaveConfig
    {
        public void Dispatch(JToken payload, AppStateManager appState, JsonConfig appSettings, JsonConfig orgSettings)
        {
            appState.Commit(new JObject { ["isLoading"] = true });
            var config = payload as JObject;
            var chromePath = (string)config?[AppConstants.PATH_CHROME];
            var orgSettingsPath = (string)config?[AppConstants.PATH_ORG_SETTINGS];
            var newchromePath = (string)config?[AppConstants.PATH_CHROME];

            Observable.Merge(
                Observable.FromAsync(() => appState.SaveOrgSettingsPathAsync(orgSettingsPath))
                .Catch((Exception ex) => Observable.Return(ex.ToString())),

                Observable.FromAsync(() => appSettings.SaveAysnc(o => o[AppConstants.PATH_CHROME] = chromePath)).Select(_ => (string)null)
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
                    appState.Commit(new JObject { ["alertMessage"] = "Save successfully." });
                }
                else
                {
                    appState.Commit(new JObject { ["alertMessage"] = message });
                }
                return Observable.Empty<Unit>();
            }))
            .Finally(() => appState.Commit(new JObject { ["isLoading"] = false }))
            .ScheduleTask();
        }
    }
}