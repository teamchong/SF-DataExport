using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SF_DataExport.Dispatcher;
using SF_DataExport.Interceptor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SF_DataExport
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var services = CreateServices();
                var provider = services.BuildServiceProvider();
                var app = provider.GetService<CommandLineApplication>();
                app.Execute(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static IServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(s => new ResourceManager());
            services.AddSingleton(s => new AppSettingsConfig(s.GetService<ResourceManager>()));
            services.AddSingleton(s => new OrgSettingsConfig(s.GetService<ResourceManager>(), s.GetService<AppSettingsConfig>()));
            services.AddSingleton(s =>
            {
                var appSettings = s.GetService<AppSettingsConfig>();
                var orgSettings = s.GetService<OrgSettingsConfig>();
                var resource = s.GetService<ResourceManager>();
                return new AppStateManager(appSettings, orgSettings, resource, s.GetService<Dictionary<string, Func<JToken, Task<JToken>>>>());
            });

            services.AddSingleton<InterceptorBase>(s => new AppPageInterceptor(s.GetService<ResourceManager>(), s.GetService<AppStateManager>()));
            services.AddSingleton<InterceptorBase>(s => new ComponentInterceptor(s.GetService<ResourceManager>(), s.GetService<AppStateManager>()));
            services.AddSingleton<InterceptorBase>(s => new RootInterceptor(s.GetService<ResourceManager>(), s.GetService<AppStateManager>()));
            services.AddSingleton<InterceptorBase>(s => new FontInterceptor(s.GetService<ResourceManager>(), s.GetService<AppStateManager>()));
            services.AddSingleton<InterceptorBase>(s => new IconInterceptor(s.GetService<ResourceManager>(), s.GetService<AppStateManager>()));
            services.AddSingleton<InterceptorBase>(s => new ImageInterceptor(s.GetService<ResourceManager>(), s.GetService<AppStateManager>()));
            services.AddSingleton<InterceptorBase>(s => new ProfilePhotoInterceptor(s.GetService<ResourceManager>(), s.GetService<OrgSettingsConfig>(), s.GetService<AppStateManager>()));

            services.AddSingleton(s => new Dictionary<string, Func<JToken, Task<JToken>>>
            {
                [nameof(AttemptLogin)] = payload => new AttemptLogin(s.GetService<AppStateManager>(), s.GetService<ResourceManager>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(DownloadExports)] = payload => new DownloadExports(s.GetService<AppStateManager>(), s.GetService<ResourceManager>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(FetchDirPath)] = payload => new FetchDirPath().DispatchAsync(payload),
                [nameof(FetchFilePath)] = payload => new FetchFilePath().DispatchAsync(payload),
                [nameof(LoginAsUser)] = payload => new LoginAsUser(s.GetService<ResourceManager>(), s.GetService<AppSettingsConfig>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(RemoveOfflineAccess)] = payload => new RemoveOfflineAccess(s.GetService<AppStateManager>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(RemoveOrg)] = payload => new RemoveOrg(s.GetService<AppStateManager>(), s.GetService<ResourceManager>(), s.GetService<AppSettingsConfig>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(SaveConfig)] = payload => new SaveConfig(s.GetService<AppStateManager>(), s.GetService<AppSettingsConfig>()).DispatchAsync(payload),
                [nameof(SetOrgSettingsPath)] = payload => new SetOrgSettingsPath(s.GetService<AppStateManager>()).DispatchAsync(payload),
                [nameof(SwitchUser)] = payload => new SwitchUser(s.GetService<AppStateManager>(), s.GetService<ResourceManager>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(ViewDownloadExports)] = payload => new ViewDownloadExports(s.GetService<ResourceManager>(), s.GetService<AppSettingsConfig>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(ViewPage)] = payload => new ViewPage(s.GetService<AppStateManager>(), s.GetService<ResourceManager>(), s.GetService<AppSettingsConfig>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(GetLimits)] = payload => new GetLimits(s.GetService<AppStateManager>(), s.GetService<ResourceManager>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
                [nameof(ViewUserPage)] = payload => new ViewUserPage(s.GetService<ResourceManager>(), s.GetService<AppSettingsConfig>(), s.GetService<OrgSettingsConfig>()).DispatchAsync(payload),
            });

            services.AddTransient<Func<JObject, AppDialog>>(s => command =>
            {
                var appSettings = s.GetService<AppSettingsConfig>();
                var chromePath = appSettings.GetString(AppConstants.PATH_CHROME);
                var appState = s.GetService<AppStateManager>();
                var interceptors = s.GetServices<InterceptorBase>().ToObservable();
                return new AppDialog(chromePath, command, appState, interceptors);
            });
            services.AddTransient(s => CreateCliApplication(s));
            return services;
        }

        static CommandLineApplication CreateCliApplication(IServiceProvider service)
        {
            Console.WriteLine(AppDomain.CurrentDomain.FriendlyName + " v" + typeof(Program).Assembly.GetName().Version);
            var resource = service.GetService<ResourceManager>();
            var appSettings = service.GetService<AppSettingsConfig>();
            var orgSettings = service.GetService<OrgSettingsConfig>();

            var cliApp = new CommandLineApplication(false);

            foreach (var org in orgSettings.Get(d => d.Properties()))
            {
                var instanceUrl = org.Name;
                var orgName = resource.GetOrgName(instanceUrl);
                var hasOfflineAccess = !string.IsNullOrEmpty((string)org.Value[OAuth.REFRESH_TOKEN]);

                if (hasOfflineAccess)
                {
                    cliApp.Command("download@" + orgName, cliCfg =>
                    {
                        var emailOpt = cliCfg.Option("--email", "Email to", CommandOptionType.SingleValue);
                        var exportPathOpt = cliCfg.Option("--path", "Export to path", CommandOptionType.SingleValue);
                        cliCfg.OnExecute(() => StartAsync(service, new JObject
                        {
                            ["command"] = AppConstants.COMMAND_DOWNLOAD,
                            ["exportEmails"] = emailOpt.Value(),
                            ["exportPath"] = exportPathOpt.Value(),
                        }));
                    });
                    cliApp.Command("loginas@" + orgName, cliCfg =>
                    {
                        var userOpt = cliCfg.Option("--user", "User Id", CommandOptionType.SingleValue);
                        var pageOpt = cliCfg.Option("--page", "Page to visit", CommandOptionType.SingleValue);
                        cliCfg.OnExecute(() => StartAsync(service, new JObject
                        {
                            ["command"] = AppConstants.COMMAND_LOGIN_AS,
                            ["userId"] = userOpt.Value(),
                            ["page"] = pageOpt.Value(),
                        }));
                    });
                    cliApp.Command("loglimits@" + orgName, cliCfg =>
                    {
                        cliCfg.OnExecute(() => StartAsync(service, new JObject
                        {
                            ["command"] = AppConstants.COMMAND_LOG_LIMITS
                        }));
                    });
                }

                cliApp.Command(orgName, cliCfg => AddCliCommandWithUI(cliCfg, new JObject { ["type"] = "AttemptLogin", ["payload"] = org.Name }));
            }

            AddCliCommandWithUI(cliApp, null);
            return cliApp;

            void AddCliCommandWithUI(CommandLineApplication app, JObject command)
            {
                var pathOpt = app.Option("--chromepath", "Chrome executable path", CommandOptionType.SingleValue);
                var channelOpt = app.Option("--chromechannel", "Preferred Chrome Channel", CommandOptionType.SingleValue);
                app.OnExecute(async () =>
                {
                    app.ShowHelp();
                    await InitializeChromeAsync(appSettings, resource, pathOpt, channelOpt).GoOn();
                    return await StartAsync(service, command).GoOn();
                });
            }
        }

        static async Task InitializeChromeAsync(AppSettingsConfig appSettings, ResourceManager resource, CommandOption pathOpt, CommandOption channelOpt)
        {
            var chromePathInConfig = appSettings.GetString(AppConstants.PATH_CHROME);
            var chromePath = GetChromePath(resource, channelOpt.Value(), pathOpt.Value(), chromePathInConfig);

            if (!string.IsNullOrEmpty(chromePath) && chromePathInConfig != chromePath)
            {
                await appSettings.SaveAysnc(cfg => cfg[AppConstants.PATH_CHROME] = chromePath).GoOn();
            }

            var (orgPath, orgPathInConfig, orgPathSave) = resource.GetOrgPath(appSettings);

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

            if (string.IsNullOrEmpty(appSettings.GetString(AppConstants.PATH_CHROME)))
            {
                resource.OpenBrowser("https://www.google.com/chrome");
                throw new Exception("Cannot find chromium installation path, please download at https://www.google.com/chrome or specific the path using the -chromepath <path> option.");
            }
        }

        static string GetChromePath(ResourceManager resource, string channelOpt, params string[] pathOpts)
        {
            var finder = new ChromeFinder(resource);

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

            var (chromePath, type) = finder.Find(channelOpt);
            if (string.IsNullOrEmpty(chromePath))
            {
                return null;
            }

            return chromePath;
        }

        static async Task<int> StartAsync(IServiceProvider service, JObject command)
        {
            var appSettings = service.GetService<AppSettingsConfig>();
            Console.WriteLine(AppConstants.JSON_APP_SETTINGS + ": " + appSettings.Get(d => d.ToString(0)));

            var processed = await service.GetService<AppStateManager>().ProcessCommandAsync(command);
            if (!processed)
            {
                await service.GetService<Func<JObject, AppDialog>>()(command).ShowAsync().GoOn();
            }

            return 0;
        }
    }
}
