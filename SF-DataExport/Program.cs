using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SF_DataExport
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var appSettings = new JsonConfig(Path.Combine(AppContext.BaseDirectory, AppConstants.APP_SETTINGS_JSON));
                var orgSettings = new JsonConfig(Path.Combine(GetOrgPath(appSettings).orgPath, AppConstants.ORG_SETTINGS_JSON));

                var cliApp = new CommandLineApplication(false);
                var replaceRex = new Regex( @"^https://|\.my\.salesforce\.com$|\.salesforce\.com$", RegexOptions.IgnoreCase);

                foreach (var org in orgSettings.Read().Properties())
                {
                    var orgName = replaceRex.Replace(org.Name, "")
                        .Replace(" ", "-").ToLower();
                    var hasOfflineAccess = !string.IsNullOrEmpty(org.Value[OAuth.REFRESH_TOKEN]?.ToString());

                    cliApp.Command(orgName, cliCfg =>
                    {
                        var pathOpt = cliCfg.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                        var channelOpt = cliCfg.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                        cliCfg.OnExecute(async () =>
                        {
                            cliApp.ShowHelp();
                            await InitializeAsync(appSettings, pathOpt, channelOpt).Continue();
                            return await StartAsync(appSettings, orgSettings, org.Name, null).Continue();
                        });
                    });

                    if (hasOfflineAccess)
                    {
                        cliApp.Command("download@" + orgName, cliCfg =>
                        {
                            var pathOpt = cliCfg.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                            var channelOpt = cliCfg.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                            var userOpt = cliCfg.Option("-u", "Salesforce User Id", CommandOptionType.SingleValue);
                            var exportPathOpt = cliCfg.Option("-p", "Export to path", CommandOptionType.SingleValue);
                            cliCfg.OnExecute(async () =>
                            {
                                await InitializeAsync(appSettings, pathOpt, channelOpt).Continue();
                                return await StartAsync(appSettings, orgSettings, "", new JObject
                                {
                                    ["command"] = "download",
                                    ["userId"] = userOpt.Value(),
                                    ["exportPath"] = exportPathOpt.Value(),
                                }).Continue();
                            });
                        });
                    }
                }

                {
                    var pathOpt = cliApp.Option("-p", "Chrome executable path", CommandOptionType.SingleValue);
                    var channelOpt = cliApp.Option("-c", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                    cliApp.OnExecute(async () =>
                    {
                        cliApp.ShowHelp();
                        await InitializeAsync(appSettings, pathOpt, channelOpt).Continue();
                        return await StartAsync(appSettings, orgSettings, "", null).Continue();
                    });
                }

                cliApp.Execute(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

#if DEBUG
            Console.WriteLine("Please any key to continue...");
            Console.ReadKey();
#endif
        }

        static async Task InitializeAsync(JsonConfig appSettings, CommandOption pathOpt, CommandOption channelOpt)
        {
            var chromePathInConfig = appSettings.GetString(AppConstants.CHROME_PATH);
            var chromePath = GetChromePath(channelOpt.Value(), pathOpt.Value(), chromePathInConfig);

            if (!string.IsNullOrEmpty(chromePath) && chromePathInConfig != chromePath)
            {
                await appSettings.SaveAysnc(cfg => cfg[AppConstants.CHROME_PATH] = chromePath).Continue();
            }

            var (orgPath, orgPathInConfig, orgPathSave) = GetOrgPath(appSettings);

            if (orgPathSave != orgPathInConfig)
            {
                await appSettings.SaveAysnc(o =>
                {
                    if (orgPathSave == "")
                        o.Remove(AppConstants.ORG_SETTINGS_PATH);
                    else
                        o[AppConstants.ORG_SETTINGS_PATH] = orgPathSave;
                }).Continue();
            }
            Console.WriteLine(AppConstants.APP_SETTINGS_JSON + ": " + appSettings.Read().ToString(0));
        }

        static (string orgPath, string orgPathInConfig, string orgPathSave) GetOrgPath(JsonConfig appSettings)
        {
            var orgPathInConfig = appSettings.GetString(AppConstants.ORG_SETTINGS_PATH);
            var orgPath = (orgPathInConfig != "" ? orgPathInConfig : null)
                ?? AppContext.BaseDirectory;
            var orgPathSave = orgPath != AppContext.BaseDirectory ? orgPath : "";
            return (orgPath, orgPathInConfig, orgPathSave);
        }

        static string GetChromePath(string channelOpt, params string[] pathOpts)
        {
            var finder = new ChromeFinder();

            if (pathOpts != null)
            {
                foreach (var pathOpt in pathOpts.Where(p => !string.IsNullOrEmpty(p)))
                {
                    if (finder.CanAccess(pathOpt))
                    {
                        return pathOpt;
                    }
                    Console.WriteLine("Cannot access chrome at " + pathOpt);
                }
            }

            var (chromePath, type) = new ChromeFinder().Find(channelOpt);
            if (string.IsNullOrEmpty(chromePath))
            {
                return null;
            }

            return chromePath;
        }

        static async Task<int> StartAsync(JsonConfig appSettings, JsonConfig orgSettings, string instanceUrl, JObject command)
        {
            if (string.IsNullOrEmpty(appSettings.GetString(AppConstants.CHROME_PATH)))
            {
                new ResourceManager().OpenBrowser("https://www.google.com/chrome");
                throw new Exception("Cannot find chromium installation path, please specific the path using the -p <path> option.");
            }

            await AppDialog.CreateAsync(appSettings, orgSettings, new ResourceManager(), instanceUrl, command).Continue();
            return 0;
        }
    }
}
