using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Rx = System.Reactive.Linq.Observable;

namespace SF_DataExport
{
    public class AppStateManager
    {
        public JsonMergeSettings MergeSettings = new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Merge,
            PropertyNameComparison = StringComparison.CurrentCulture,
        };

        ResourceManager Resource { get; set; }
        JsonConfig AppSettings { get; set; }
        JsonConfig OrgSettings { get; set; }
        JObject AppState { get; set; }
        Subject<JObject> CommitSubject = new Subject<JObject>();

        public JObject Value { get => AppState; }

        public AppStateManager(JsonConfig appSettings, JsonConfig orgSettings, ResourceManager resource)
        {
            Resource = resource;
            AppSettings = appSettings;
            OrgSettings = orgSettings;
            AppState = new JObject
            {
                ["appPage"] = "index",
                ["chromePath"] =AppSettings.Get(o => o["chromePath"])?.ToString() ?? "",
                ["currentInstanceUrl"] = "",
                ["isLoading"] = false,
                ["showOrgModal"] = true,
                ["showUserPopover"] = true,
                ["orgSettingsPath"] = OrgSettings.GetDirectoryPath(),
                ["tab"] = "downloaddataexport",
                ["userDisplayName"] = "",
                ["userEmail"] = "",
                ["userId"] = "",
                ["userName"] = "",
                ["userPhoto"] = "",
                ["userExportPath"] = "",
                ["userExportSelection"] = "",
                ["userPopoverSelection"] = "",
                ["users"] = new JArray(),
            };
            AppState.Merge(GetOrgSettings(), MergeSettings);
        }

        public void Subscribe(Page appPage)
        {
            CommitSubject.Buffer(TimeSpan.FromMilliseconds(100)).Where(newStates => newStates.Any())
                .Select(newStates =>
                    Rx.FromAsync(() => appPage.EvaluateFunctionAsync("storeCommit", new JArray(newStates)))
                    .Catch((Exception ex) => Rx.Start(() => Console.WriteLine(ex.ToString())).SelectMany(_ => Rx.Empty<JToken>()))
                 )
                .Concat()
                .SubscribeOn(TaskPoolScheduler.Default)
                .Subscribe();
        }

        public JObject GetOrgSettings()
        {
            var orgList = OrgSettings.List();
            var orgOfflineAccess = new JArray(orgList
                    .Where(org => !string.IsNullOrEmpty(OrgSettings.Get(o => o[org]?[OAuth.REFRESH_TOKEN])?.ToString())));
            var orgSandboxes = new JArray(orgList
                    .Where(org => Resource.IsSandboxLoginUrl(OrgSettings.Get(o => o[org]?[OAuth.LOGIN_URL])?.ToString())));
            return new JObject
            {
                ["orgOfflineAccess"] = orgOfflineAccess,
                ["orgSandboxes"] = orgSandboxes,
                ["orgSettings"] = new JArray(orgList),
            };
        }

        public void Commit(JObject newState)
        {
            if (newState != null)
            {
                AppState.Merge(newState, MergeSettings);
                CommitSubject.OnNext(newState);
            }
        }
    }
}
