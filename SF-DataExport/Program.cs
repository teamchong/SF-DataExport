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

            var cliApp = new CommandLineApplication(false);

            cliApp.Command("downloaddataexport", cliCfg =>
            {
                var pathOpt = cliApp.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                var channelOpt = cliApp.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                cliCfg.OnExecute(async () =>
                {
                    var chromePathInConfig= appSettings.Get(o => o["chromePath"]?.ToString());
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
                var orgPathOpt = cliApp.Option("-o", "orgsettings.json path", CommandOptionType.SingleValue);

                cliApp.OnExecute(async () =>
                {
                    cliApp.ShowHelp();
                    
                    var chromePathInConfig= appSettings.Get(o => o["chromePath"]?.ToString()) ?? "";
                    var chromePath = GetChromePath(pathOpt.Value(), channelOpt.Value(), chromePathInConfig);
                    if (!string.IsNullOrEmpty(pathOpt.Value()) && chromePathInConfig != chromePath)
                    {
                        await appSettings.SaveAysnc(cfg => cfg["chromePath"] = chromePath);
                    }

                    var orgPathInConfig = appSettings.Get(o => o["orgSettingsPath"]?.ToString()) ?? "";
                    var orgPath = Path.Combine(
                        (orgPathOpt.Value() != "" ? orgPathOpt.Value() : null)
                        ?? (orgPathInConfig != "" ? orgPathInConfig : null)
                        ?? AppContext.BaseDirectory,
                        "orgsettings.json");
                    var orgSettings = new JsonConfig(orgPath);

                    var orgPathSave = orgPath != Path.Combine(AppContext.BaseDirectory, "orgsettings.json") ? orgPath : "";
                    if (orgPathSave != orgPathInConfig) await appSettings.SaveAysnc(o => o["orgSettingsPath"] = orgPathSave);


                    if (!string.IsNullOrEmpty(pathOpt.Value()) && chromePathInConfig != chromePath)
                    {
                        await appSettings.SaveAysnc(cfg => cfg["orgSettingsPath"] = chromePath);
                    }
                    
                    return await StartAsync(chromePath, appSettings, orgSettings);
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

        static async Task<int> StartAsync(string chromePath, JsonConfig appSettings, JsonConfig orgSettings)
        {
            if (string.IsNullOrEmpty(chromePath))
            {
                new ResourceHelper().OpenBrowser("https://www.google.com/chrome");
                throw new Exception("Cannot find chromium installation path, please specific the path using the -p <path> option.");
            }
            
            var appDialog = new AppDialog(appSettings, orgSettings);
            await appDialog.DisplayAsync(chromePath);
            return 0;
        }
    }
}
