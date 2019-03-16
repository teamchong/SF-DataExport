using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace SF_DataExport.Dispatcher
{
    public class FetchPath
    {
        public void Dispatch(JToken payload, AppStateManager appState)
        {
            var search = ((string)payload?["search"])?.Trim() ?? "";
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
                            [field] = new JArray(searchDir.GetFiles().Select(f => f.FullName)
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
                            if (fi.Exists)
                            {
                                appState.Commit(new JObject
                                {
                                    [field] = new JArray(new[] { fi.FullName }
                                        .Concat(fi.Directory.GetFiles().Where(f => MatchFile(f, fi.Name)).Select(f => f.FullName))
                                        .Concat(fi.Directory.GetDirectories().Where(d => MatchDir(d, fi.Name)).Select(d => TrimDir(d.FullName)))
                                        .Where(s => s != "").Distinct())
                                });
                                return;
                            }
                            else
                            {
                                appState.Commit(new JObject
                                {
                                    [field] = new JArray(fi.Directory.GetFiles().Where(f => MatchFile(f, fi.Name)).Select(f => f.FullName)
                                        .Concat(fi.Directory.GetDirectories().Where(d => MatchDir(d, fi.Name)).Select(d => TrimDir(d.FullName)))
                                        .Where(s => s != "").Distinct())
                                });
                                return;
                            }
                        }
                    }
                }
                catch { }
            }
            appState.Commit(new JObject { [field] = new JArray() });
            
            
            string TrimDir(string dir)
            {
                return (dir?.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "") + Path.DirectorySeparatorChar;
            }

            bool MatchFile(FileInfo f, string filterText)
            {
                return !string.IsNullOrEmpty(filterText) && f.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase);
            }

            bool MatchDir(DirectoryInfo di, string filterText)
            {
                return !string.IsNullOrEmpty(filterText) && di.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }
}