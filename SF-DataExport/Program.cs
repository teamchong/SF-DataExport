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
                Console.WriteLine(AppDomain.CurrentDomain.FriendlyName + " v" + typeof(Program).Assembly.GetName().Version);
                var appSettings = new JsonConfig(Path.Combine(AppContext.BaseDirectory, AppConstants.JSON_APP_SETTINGS));
                var orgSettings = new JsonConfig(Path.Combine(GetOrgPath(appSettings).orgPath, AppConstants.JSON_ORG_SETTINGS));

                var cliApp = new CommandLineApplication(false);
                var replaceRex = new Regex(@"^https://|\.my\.salesforce\.com$|\.salesforce\.com$", RegexOptions.IgnoreCase);

                foreach (var org in orgSettings.Get(d => d.Properties()))
                {
                    var orgName = replaceRex.Replace(org.Name, "")
                        .Replace(" ", "-").ToLower();
                    var hasOfflineAccess = !string.IsNullOrEmpty((string)org.Value[OAuth.REFRESH_TOKEN]);

                    cliApp.Command(orgName, cliCfg =>
                    {
                        var pathOpt = cliCfg.Option("-chromepath", "Chrome executable path", CommandOptionType.SingleValue);
                        var channelOpt = cliCfg.Option("-chromechannel", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                        cliCfg.OnExecute(async () =>
                        {
                            cliApp.ShowHelp();
                            await InitializeAsync(appSettings, pathOpt, channelOpt).GoOn();
                            return await StartAsync(appSettings, orgSettings, org.Name, null).GoOn();
                        });
                    });

                    if (hasOfflineAccess)
                    {
                        cliApp.Command("download@" + orgName, cliCfg =>
                        {
                            var pathOpt = cliCfg.Option("-chromepath", "Chrome executable path", CommandOptionType.SingleValue);
                            var channelOpt = cliCfg.Option("-chromechannel", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                            var emailOpt = cliCfg.Option("-email", "Email to", CommandOptionType.SingleValue);
                            var exportPathOpt = cliCfg.Option("-path", "Export to path", CommandOptionType.SingleValue);
                            cliCfg.OnExecute(async () =>
                            {
                                await InitializeAsync(appSettings, pathOpt, channelOpt).GoOn();
                                return await StartAsync(appSettings, orgSettings, "", new JObject
                                {
                                    ["command"] = AppConstants.COMMAND_DOWNLOAD,
                                    ["instanceUrl"] = org.Name,
                                    ["exportEmails"] = emailOpt.Value(),
                                    ["exportPath"] = exportPathOpt.Value(),
                                }).GoOn();
                            });
                        });
                    }
                }

                {
                    var pathOpt = cliApp.Option("-chromepath", "Chrome executable path", CommandOptionType.SingleValue);
                    var channelOpt = cliApp.Option("-chromechannel", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                    cliApp.OnExecute(async () =>
                    {
                        cliApp.ShowHelp();
                        await InitializeAsync(appSettings, pathOpt, channelOpt).GoOn();
                        return await StartAsync(appSettings, orgSettings, "", null).GoOn();
                    });
                }

                cliApp.Execute(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static async Task InitializeAsync(JsonConfig appSettings, CommandOption pathOpt, CommandOption channelOpt)
        {
            var chromePathInConfig = appSettings.GetString(AppConstants.PATH_CHROME);
            var chromePath = GetChromePath(channelOpt.Value(), pathOpt.Value(), chromePathInConfig);

            if (!string.IsNullOrEmpty(chromePath) && chromePathInConfig != chromePath)
            {
                await appSettings.SaveAysnc(cfg => cfg[AppConstants.PATH_CHROME] = chromePath).GoOn();
            }

            var (orgPath, orgPathInConfig, orgPathSave) = GetOrgPath(appSettings);

            if (orgPathSave != orgPathInConfig)
            {
                await appSettings.SaveAysnc(o =>
                {
                    if (orgPathSave == "")
                    {
                        o.Remove(AppConstants.PATH_ORG_SETTINGS);
                    }
                    else
                    {
                        o[AppConstants.PATH_ORG_SETTINGS] = orgPathSave;
                    }
                }).GoOn();
            }
            Console.WriteLine(AppConstants.JSON_APP_SETTINGS + ": " + appSettings.Get(d => d.ToString(0)));
        }

        static (string orgPath, string orgPathInConfig, string orgPathSave) GetOrgPath(JsonConfig appSettings)
        {
            var orgPathInConfig = appSettings.GetString(AppConstants.PATH_ORG_SETTINGS);
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
            if (string.IsNullOrEmpty(appSettings.GetString(AppConstants.PATH_CHROME)))
            {
                new ResourceManager().OpenBrowser("https://www.google.com/chrome");
                throw new Exception("Cannot find chromium installation path, please specific the path using the -chromepath <path> option.");
            }

            await AppDialog.CreateAsync(appSettings, orgSettings, new ResourceManager(), instanceUrl, command).GoOn();
            return 0;
        }
    }
}
