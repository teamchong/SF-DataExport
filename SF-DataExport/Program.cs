using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
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
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            var cliApp = new CommandLineApplication(false);

            cliApp.Command("downloaddataexport", cliCfg =>
            {
                var pathOpt = cliApp.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                var channelOpt = cliApp.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                cliCfg.OnExecute(async () =>
                {
                    return await DownloadDataExportAsync(await GetChromePath(pathOpt.Value(), channelOpt.Value(), config));
                });
            });

            {
                var pathOpt = cliApp.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                var channelOpt = cliApp.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                var orgPathOpt = cliApp.Option("-o", "organization.json path", CommandOptionType.SingleValue);
                cliApp.OnExecute(async () =>
                {
                    cliApp.ShowHelp();
                    return await StartAsync(
                        await GetChromePath(pathOpt.Value(), channelOpt.Value(), config),
                        orgPathOpt.Value() ?? AppContext.BaseDirectory);
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

        static async Task<string> GetChromePath(string pathOpt, string channelOpt, IConfigurationRoot config)
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

            if (finder.CanAccess(config["chromePath"]))
            {
                return config["chromePath"];
            }

            var (chromePath, type) = new ChromeFinder().Find(channelOpt);
            if (string.IsNullOrEmpty(chromePath))
            {
                return null;
            }

            if (config["chromePath"] != chromePath)
            {
                await WriteJsonAsync(cfg => cfg["chromePath"] = chromePath);
            }
            return chromePath;
        }

        static async Task WriteJsonAsync(Action<JObject> setter)
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var configJson = new JObject();
            try
            {
                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        configJson = JsonConvert.DeserializeObject<JObject>(await reader.ReadToEndAsync());
                    }
                }
            }
            catch { }
            setter(configJson);
            await File.WriteAllTextAsync(configPath, configJson.ToString());
        }

        static async Task<int> DownloadDataExportAsync(string chromePath)
        {
            return 0;
        }

        static async Task<int> StartAsync(string chromePath, string orgPath)
        {
            if (string.IsNullOrEmpty(chromePath))
            {
                new ResourceHelper().OpenBrowser("https://www.google.com/chrome");
                throw new Exception("Cannot find chromium installation path, please specific the path using the -p <path> option.");
            }

            var appDialog = new AppDialog(orgPath);
            await appDialog.DisplayAsync(chromePath);
            return 0;
        }
    }
}
