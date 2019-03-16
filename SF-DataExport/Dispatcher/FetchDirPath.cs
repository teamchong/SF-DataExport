using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace SF_DataExport.Dispatcher
{
    public class FetchDirPath
    {
        public void Dispatch(JToken payload, AppStateManager appState)
        {
            var search = TrimDir((string)payload?["search"]);
            var field = ((string)payload?["field"]) ?? "";

            if (search != "" && field != "")
            {
                try
                {
                    var searchDir = new DirectoryInfo(search);
                    if (searchDir.Exists)
                    {
                        appState.Commit(new JObject
                        {
                            [field] = new JArray(new[] { TrimDir(searchDir.FullName), TrimDir(searchDir.Parent.FullName) }
                            .Concat(searchDir.Parent.GetDirectories().Where(d => MatchDir(d, searchDir.Name)).Select(d => TrimDir(d.FullName)))
                            .Concat(searchDir.GetDirectories().Select(d => TrimDir(d.FullName)))
                            .Where(s => s != "").Distinct())
                        });
                        return;
                    }
                    else
                    {
                        var fi = new FileInfo(search);
                        if (fi.Directory.Exists)
                        {
                            appState.Commit(new JObject
                            {
                                [field] = new JArray(new[] { TrimDir(fi.Directory.FullName) }
                                .Concat(fi.Directory.GetDirectories().Where(d => MatchDir(d, fi.Name)).Select(d => TrimDir(d.FullName)))
                                .Where(s => s != "").Distinct())
                            });
                            return;
                        }
                    }
                }
                catch { }
            }
            appState.Commit(new JObject { [field] = new JArray() });

            string TrimDir(string dir)
            {
                return dir?.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "";
            }

            bool MatchDir(DirectoryInfo di, string filterText)
            {
                return !string.IsNullOrEmpty(filterText) && di.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }
}