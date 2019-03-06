using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SF_DataExport
{
    class ChromeFinder
    {
        Regex NewLineRegex = new Regex("\\r?\\n");
        ResourceManager Resource {  get; set; }

        public ChromeFinder()
        {
            Resource = new ResourceManager();
        }

        public (string executablePath, string type) Find(string channel)
        {
            var config = channel?.Split(' ').Where(s => s != "").ToHashSet() ?? new HashSet<string>();
            if (config.Count <= 0) config.Add("stable");

            string chromePath = null;

            // Always prefer canary
            if (config.Contains("canary") || config.Contains("*"))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    chromePath = FindChromeInLinux(true);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    chromePath = FindChromeInOSX(true);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    chromePath = FindChromeInWin32(true);

                if (!string.IsNullOrEmpty(chromePath))
                    return (chromePath, "canary");
            }

            // Then pick stable.
            if (config.Contains("stable") || config.Contains("*"))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    chromePath = FindChromeInLinux(false);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    chromePath = FindChromeInOSX(false);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    chromePath = FindChromeInWin32(false);

                if (!string.IsNullOrEmpty(chromePath))
                    return (chromePath, "stable");
            }


            return (chromePath, "");
        }

        string FindChromeInLinux(bool canary)
        {
            var installations = new HashSet<string>();

            // Look into the directories where .desktop are saved on gnome based distro's
            var desktopInstallationFolders = new List<string>
            {
                Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".local/share/applications/"),
                "/usr/share/applications/",
            };
            desktopInstallationFolders.ForEach(folder =>
            {
                foreach (var installation in FindChromeExecutables(folder))
                {
                    installations.Add(installation);
                }
            });

            // Look for google-chrome(-stable) & chromium(-browser) executables by using the which command
            var executables = new List<string>
            {
                "google-chrome-stable",
                "google-chrome",
                "chromium-browser",
                "chromium",
            };
            executables.ForEach(executable =>
            {
                try
                {
                    var chromePath = NewLineRegex.Split(Resource.Execute("which executable")).FirstOrDefault();
                    if (CanAccess(chromePath))
                        installations.Add(chromePath);
                }
                catch
                {
                    // Not installed.
                }
            });

            if (installations.Count <= 0)
                throw new Exception("The environment variable CHROME_PATH must be set to executable of a build of Chromium version 54.0 or later.");

            var priorities = new List<(Regex regex, int weight)>
            {
                (new Regex(" chrome - wrapper$"), 51),
                (new Regex("google-chrome-stable$"), 50),
                (new Regex("google-chrome$"), 49),
                (new Regex("chromium-browser$"), 48),
                (new Regex("chromium$"), 47),
            };

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CHROME_PATH")))
                priorities.Insert(0, (new Regex(Environment.GetEnvironmentVariable("CHROME_PATH")), 101));

            return installations.Where(s => !string.IsNullOrEmpty(s)).Distinct()
                .OrderBy(s => priorities.Where(p => p.regex.IsMatch(s)).Select(p => p.weight).FirstOrDefault() )
                .FirstOrDefault();
        }

        string FindChromeInOSX(bool canary)
        {
            var LSREGISTER = "/System/Library/Frameworks/CoreServices.framework" +
                 "/Versions/A/Frameworks/LaunchServices.framework" +
                 "/Versions/A/Support/lsregister";
            var grepexpr = canary ? "google chrome canary" : "google chrome";
            var result = Resource.Execute($"{LSREGISTER} -dump  | grep -i '{grepexpr}\\?.app$' | awk '{{$1=\"\"; print $0}}'");

            var installations = new HashSet<string>();
            var paths = NewLineRegex.Split(result ?? "").Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            paths.Insert(0, canary ? "/Applications/Google Chrome Canary.app" : "/Applications/Google Chrome.app");
            foreach (var path in paths.Where(p => !p.StartsWith("/Volumes")))
            {
                var inst = Path.Combine(path, canary ? "/Contents/MacOS/Google Chrome Canary" : "/Contents/MacOS/Google Chrome");

                if (CanAccess(inst))
                    return inst;
            }
            return null;
        }

        string FindChromeInWin32(bool canary)
        {
            var suffix = canary ?
                Path.Combine("Google", "Chrome SxS", "Application", "chrome.exe") :
                Path.Combine("Google", "Chrome", "Application", "chrome.exe");
            var prefixes = new List<string>
            {
                Environment.GetEnvironmentVariable("LocalAppData"),
                Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            }.Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
            
            foreach (var prefix in prefixes)
            {
                var chromePath = Path.Combine(prefix, suffix);
                if (CanAccess(chromePath))
                {
                    return chromePath;
                }
            }
            return null;
        }

        IEnumerable<string> FindChromeExecutables(string folder)
        {
            var argumentsRegex = new Regex(" (^[^ ] +).*"); // Take everything up to the first space
            var chromeExecRegex = "^Exec=/.*/(google-chrome|chrome|chromium)-.*";

            var installations = new HashSet<string>();
            if (CanAccess(folder))
            {
                // Output of the grep & print looks like:
                //    /opt/google/chrome/google-chrome --profile-directory
                //    /home/user/Downloads/chrome-linux/chrome-wrapper %U
                string execPath = null;

                // Some systems do not support grep -R so fallback to -r.
                // See https://github.com/GoogleChrome/chrome-launcher/issues/46 for more context.
                try
                {
                    execPath = Resource.Execute($"grep - ER \"{chromeExecRegex}\" {folder} | awk - F '=' '{{print $2}}'");
                }
                catch
                {
                    execPath = Resource.Execute($"grep - Er \"{chromeExecRegex}\" {folder} | awk - F '=' '{{print $2}}'");
                }

                var execPaths = NewLineRegex.Split(execPath ?? "").Select(path => argumentsRegex.Replace(path, "$1"));
                foreach (var path in execPaths.Where(path => CanAccess(path)))
                {
                    installations.Add(path);
                }
            }

            return installations;
        }

        public bool CanAccess(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
                return false;

            try
            {
                if (!File.Exists(file)) return false;
                File.OpenRead(file).Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
