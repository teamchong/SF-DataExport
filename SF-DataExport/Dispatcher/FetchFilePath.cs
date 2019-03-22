using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reactive.Linq;

namespace SF_DataExport.Dispatcher
{
    public class FetchFilePath : IDispatcher
    {
        public Task<JToken> DispatchAsync(JToken payload)
        {
            var search = ((string)payload?["search"])?.Trim() ?? "";

            if (search != "")
            {
                try
                {
                    var searchDir = new DirectoryInfo(search);

                    if (searchDir.Exists)
                    {
                        return Task.FromResult<JToken>(new JArray(searchDir.GetFiles().Select(f => f.FullName)
                            .Concat(searchDir.GetDirectories().Select(d => TrimDir(d.FullName)))
                            .Where(s => s != "").Distinct()));
                    }
                    else
                    {
                        var fi = new FileInfo(search);

                        if (fi.Directory?.Exists == true)
                        {
                            if (fi.Exists)
                            {
                                return Task.FromResult<JToken>(new JArray(new[] { fi.FullName }
                                    .Concat(fi.Directory.GetFiles().Where(f => MatchFile(f, fi.Name)).Select(f => f.FullName))
                                    .Concat(fi.Directory.GetDirectories().Where(d => MatchDir(d, fi.Name)).Select(d => TrimDir(d.FullName)))
                                    .Concat(Directory.GetLogicalDrives().Select(d => TrimDir(d)))
                                    .Where(s => s != "").Distinct()));
                            }
                            else
                            {
                                return Task.FromResult<JToken>(new JArray(fi.Directory.GetFiles().Where(f => MatchFile(f, fi.Name)).Select(f => f.FullName)
                                    .Concat(fi.Directory.GetDirectories().Where(d => MatchDir(d, fi.Name)).Select(d => TrimDir(d.FullName)))
                                    .Concat(Directory.GetLogicalDrives().Select(d => TrimDir(d)))
                                    .Where(s => s != "").Distinct()));
                            }
                        }
                    }
                }
                catch { }
            }
            return Task.FromResult<JToken>(new JArray(Directory.GetLogicalDrives().Select(d => TrimDir(d))));


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