using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace SF_DataExport.Dispatcher
{
    public class FetchDirPath
    {
        public JToken Dispatch(JToken payload, AppStateManager appState)
        {
            var search = TrimDir((string)payload?["search"]);

            if (search != "")
            {
                try
                {
                    var searchDir = new DirectoryInfo(search);
                    if (searchDir.Exists)
                    {
                        return new JArray(new[] { TrimDir(searchDir.FullName), TrimDir(searchDir.Parent?.FullName) }
                            .Concat(searchDir.Parent?.GetDirectories().Where(d => MatchDir(d, searchDir.Name)).Select(d => TrimDir(d.FullName)) ?? new string[0])
                            .Concat(searchDir.GetDirectories().Select(d => TrimDir(d.FullName)))
                                .Concat(Directory.GetLogicalDrives().Select(d => TrimDir(d)))
                            .Where(s => s != "").Distinct());
                    }
                    else
                    {
                        var fi = new FileInfo(search);
                        if (fi.Directory?.Exists == true)
                        {
                            return new JArray(new[] { TrimDir(fi.Directory.FullName) }
                                .Concat(fi.Directory.GetDirectories().Where(d => MatchDir(d, fi.Name)).Select(d => TrimDir(d.FullName)))
                                .Concat(Directory.GetLogicalDrives().Select(d => TrimDir(d)))
                                .Where(s => s != "").Distinct());
                        }
                    }
                }
                catch { }
            }
            return new JArray(Directory.GetLogicalDrives().Select(d => TrimDir(d)));

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