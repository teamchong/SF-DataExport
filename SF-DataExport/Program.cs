using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport
{
    class Program
    {
        static void Main(string[] args)
        {
            var appSettings = new JsonConfig(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

            var cliApp = new CommandLineApplication(true);

            cliApp.Command("downloaddataexport", cliCfg =>
            {
                var pathOpt = cliApp.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                var channelOpt = cliApp.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                cliCfg.OnExecute(async () =>
                {
                    var chromePathInConfig = appSettings.Get(o => o["chromePath"]?.ToString());
                    var chromePath = GetChromePath(pathOpt.Value(), channelOpt.Value(), chromePathInConfig);
                    if (!string.IsNullOrEmpty(pathOpt.Value()) && chromePathInConfig != chromePath)
                    {
                        await appSettings.SaveAysnc(cfg => cfg["chromePath"] = chromePath);
                    }
                    return await DownloadDataExportAsync(chromePath);
                });
            });

            {
                var pathOpt = cliApp.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                var channelOpt = cliApp.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);

                cliApp.OnExecute(async () =>
                {
                    cliApp.ShowHelp();

                    var chromePathInConfig = appSettings.Get(o => o["chromePath"]?.ToString()) ?? "";
                    var chromePath = GetChromePath(pathOpt.Value(), channelOpt.Value(), chromePathInConfig);
                    if (!string.IsNullOrEmpty(pathOpt.Value()) && chromePathInConfig != chromePath)
                    {
                        await appSettings.SaveAysnc(cfg => cfg["chromePath"] = chromePath);
                    }

                    var orgPathInConfig = appSettings.Get(o => o["orgSettingsPath"]?.ToString()) ?? "";
                    var orgPath = (orgPathInConfig != "" ? orgPathInConfig : null)
                        ?? AppContext.BaseDirectory;
                    var orgSettings = new JsonConfig(Path.Combine(orgPath,"orgsettings.json"));

                    var orgPathSave = orgPath != AppContext.BaseDirectory ? orgPath : "";
                    if (orgPathSave != orgPathInConfig)
                    {
                        await appSettings.SaveAysnc(o =>
                        {
                            if (orgPathSave == "")
                                o.Remove("orgSettingsPath");
                            else
                                o["orgSettingsPath"] = orgPathSave;
                        });
                    }

                    return await StartAsync(appSettings, orgSettings);
                });
            }

            cliApp.Execute();
        }

        //static int Launch(string executablePath, string channel, CommandLineApplication cliApp)
        //{
        //    var (chromePath, type) = new ChromeFinder().Find(executablePath, channel);
        //    if (string.IsNullOrEmpty(chromePath))
        //    {
        //        ProcessHelper.I.OpenBrowser("https://www.google.com/chrome");
        //        throw new Exception("Cannot find chrome installation path, please specific the path using the -p <project> option.");
        //    }
        //    cliApp.ShowHelp();
        //    return 0;
        //}

        static string GetChromePath(string pathOpt, string channelOpt, string chromePathInConfig)
        {
            var finder = new ChromeFinder();

            if (!string.IsNullOrEmpty(pathOpt))
            {
                if (finder.CanAccess(pathOpt))
                {
                    return pathOpt;
                }
                throw new Exception("Cannot access chrome " + pathOpt);
            }

            if (finder.CanAccess(chromePathInConfig))
            {
                return chromePathInConfig;
            }

            var (chromePath, type) = new ChromeFinder().Find(channelOpt);
            if (string.IsNullOrEmpty(chromePath))
            {
                return null;
            }

            return chromePath;
        }

        static async Task<int> DownloadDataExportAsync(string chromePath)
        {
            return 0;
        }

        static async Task<int> StartAsync(JsonConfig appSettings, JsonConfig orgSettings)
        {
            if (string.IsNullOrEmpty(appSettings.Get(o => o["chromePath"])?.ToString()))
            {
                new ResourceManager().OpenBrowser("https://www.google.com/chrome");
                throw new Exception("Cannot find chromium installation path, please specific the path using the -p <path> option.");
            }

            await AppDialog.CreateAsync(appSettings, orgSettings, new ResourceManager());
            return 0;
        }
    }
}
