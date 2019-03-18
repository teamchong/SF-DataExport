using CsvHelper;
using CsvHelper.Configuration;
using DotNetForce;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport
{
    public class ResourceManager
    {
        public string CONTENT_HTML_START { get; protected set; }

        public string CONTENT_HTML_END { get; protected set; }

        SemaphoreSlim Throttler { get; set; }
        BehaviorSubject<(DateTime cacheTime, CookieContainer cookies, string instanceUrl, string accessToken)> LatestSession { get; set; }

        public ResourceManager()
        {
            var asm = typeof(ResourceManager).Assembly;
            var resList = asm.GetManifestResourceNames();
            CONTENT_HTML_START = string.Join("", @"<html>
<head>
<title>Salesforce DataExport</title>
<link rel='shortcut icon' type='image/x-icon' href='/favicon.ico'/>
<link rel='stylesheet' type='text/css' href='/material-icons.css'/>
<link rel='stylesheet' type='text/css' href='/vuetify.css'/>
<link rel='stylesheet' type='text/css' href='/slds.css'/>
<link rel='stylesheet' type='text/css' href='/orgchart.css'/>
<link rel='stylesheet' type='text/css' href='/font-awesome.css'/>
<link rel='stylesheet' type='text/css' href='/app.css'/>
</head>
<body>
<script>const appState=");
            CONTENT_HTML_END = string.Join("", @"
</script>", GetResource("app.html"), @"
</body>
</html>");
            Throttler = new SemaphoreSlim(1, 1);
            LatestSession = new BehaviorSubject<(DateTime cacheTime, CookieContainer cookies, string instanceUrl, string accessToken)>(
                (DateTime.Now, new CookieContainer(), "", ""));
        }

        public string OrgName(string instanceUrl)
        {
            var replaceRex = new Regex(@"^https://|\.my\.salesforce\.com$|\.salesforce\.com$", RegexOptions.IgnoreCase);
            return replaceRex.Replace(instanceUrl ?? "", "").Replace(" ", "-").ToLower();
        }

        public async Task<JArray> GetOrgLimitsLogAsync(string orgName)
        {
            var file = new FileInfo(Path.Combine(DefaultDirectory, orgName + ".limits.csv"));
            var result = new JArray();
            if (file.Exists)
            {
                var csvConfig = new Configuration { Delimiter = "\t", Encoding = Encoding.Unicode };
                using (var fileStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var streamReader = new StreamReader(fileStream, true))
                    {
                        using (var reader = new CsvReader(streamReader, csvConfig))
                        {
                            if (await reader.ReadAsync().GoOn())
                            {
                                while (await reader.ReadAsync().GoOn())
                                {
                                    result.Add(new JArray(
                                        reader.GetField<string>(0),
                                        reader.GetField<double>(1),
                                        reader.GetField<double>(2),
                                        reader.GetField<string>(3)
                                    ));
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        public void ResetCookie()
        {
            LatestSession.OnNext((DateTime.Now, new CookieContainer(), "", ""));
        }

        public async Task<CookieContainer> GetCookieAsync(string newInstanceUrl, string newAccessToken)
        {
            await Throttler.WaitAsync();
            var cookieContainer = new CookieContainer();

            try
            {
                var (cacheTime, cookies, instanceUrl, accessToken) = LatestSession.Value;
                if (instanceUrl == newInstanceUrl && accessToken == newAccessToken && cacheTime > DateTime.Now.AddMinutes(-5))
                {
                    return cookies;
                }
                var targetUrl = newInstanceUrl;
                var urlWithAccessCode = GetUrlViaAccessToken(newInstanceUrl, newAccessToken, targetUrl);

                using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                {
                    using (var httpClient = new HttpClient(handler))
                    {
                        var htmlContent = await GetLoginWaitForRedirect(httpClient, newInstanceUrl, urlWithAccessCode, targetUrl).GoOn();
                        if (!htmlContent.Contains(newInstanceUrl)) throw new UnauthorizedAccessException();
                        LatestSession.OnNext((DateTime.Now, cookieContainer, newInstanceUrl, newAccessToken));
                        return cookieContainer;
                    }
                }
            }
            finally
            {
                Throttler.Release();
            }
        }

        public Task<T> RunClientAsUserAsync<T>(Func<HttpClient, CookieContainer, string, Task<T>> funcAsync, string instanceUrl, string accessToken, string targetUrl, string id, string userId)
        {
            var targetUrlAsUser = GetLoginUrlAs(instanceUrl, id, userId, targetUrl);
            return RunClientAsync(funcAsync, instanceUrl, accessToken, targetUrlAsUser);
        }

        public async Task<T> RunClientAsync<T>(Func<HttpClient, CookieContainer, string, Task<T>> funcAsync, string instanceUrl, string accessToken, string targetUrl)
        {
            var cookieContainer = await GetCookieAsync(instanceUrl, accessToken);

            using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            {
                using (var httpClient = new HttpClient(handler))
                {
                    var htmlContent = await httpClient.GetStringAsync(targetUrl).GoOn();
                    return await funcAsync(httpClient, cookieContainer, htmlContent).GoOn();
                }
            }
        }

        public string Execute(string exeFileName)
        {
            var process = new Process();
            var outputStringBuilder = new StringBuilder();

            try
            {
                process.StartInfo.FileName = exeFileName;
                process.StartInfo.WorkingDirectory = DefaultDirectory;
                // process.StartInfo.Arguments = args;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.EnableRaisingEvents = false;
                process.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var processExited = process.WaitForExit(1000 * 60);

                if (processExited == false) // we timed out...
                {
                    process.Kill();
                    throw new Exception("ERROR: Process took too long to finish");
                }
                else if (process.ExitCode != 0)
                {
                    var output = outputStringBuilder.ToString();
                    throw new Exception("Process exited with non-zero exit code of: " + process.ExitCode + Environment.NewLine +
                        "Output from process: " + outputStringBuilder.ToString());
                }
            }
            finally
            {
                process.Close();
            }
            return outputStringBuilder.ToString();
        }

        public void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public void OpenIncognitoBrowser(string url, string chromePath)
        {
            var process = new ProcessStartInfo(chromePath,
                string.Join(" ", new[] {
                $"--bwsi", //Indicates that the browser is in "browse without sign-in" (Guest session) mode. Should completely disable extensions, sync and bookmarks. 
                $"--incognito "
                //$"--ignore-certificate-errors"
            }) + url);
            Process.Start(process);
        }

        public string GetResource(string resPath)
        {
            var stream = GetResourceStream(resPath);
            if (stream != null)
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            return null;
        }

        public byte[] GetResourceBytes(string resPath)
        {
            var stream = GetResourceStream(resPath);
            if (stream != null)
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            return null;
        }

        public bool IsSandboxLoginUrl(string loginUrl)
        {
            return loginUrl?.Contains("test.salesforce.com") == true;
        }

        public bool IsLoginUrl(string url)
        {
            return url.StartsWith("https://login.salesforce.com/") || url.StartsWith("https://test.salesforce.com/");
        }

        public bool IsRedirectPage(string url)
        {
            switch (url)
            {
                case OAuth.REDIRECT_URI:
                case OAuth.REDIRECT_URI_SANDBOX:
                    return true;
                default:
                    return false;
            }
        }

        public string GetClientIdByLoginUrl(string loginUrl)
        {
            if (IsSandboxLoginUrl(loginUrl))
                return OAuth.CLIENT_ID_SANDBOX;
            else
                return OAuth.CLIENT_ID;
        }

        public string GetRedirectUrlByLoginUrl(string loginUrl)
        {
            if (IsSandboxLoginUrl(loginUrl))
                return OAuth.REDIRECT_URI_SANDBOX;
            else
                return OAuth.REDIRECT_URI;
        }

        public string GetLoginUrlAs(string instanceUrl, string id, string userId, string url)
        {
            if (userId?.StartsWith("005") != true || string.IsNullOrEmpty(id)) return null;
            var match = Regex.Match(id, "/(00D[^/]+)/(005[^/]+)$");
            if (match == null) return null;
            var orgId = match.Groups[1].Value;
            var usrId = match.Groups[2].Value;
            if (DNF.ToID15(usrId) == DNF.ToID15(userId))
            {
                return url.StartsWith('/') ? instanceUrl + url : url;
            }
            return instanceUrl + "/servlet/servlet.su?oid=" +
                HttpUtility.UrlEncode(orgId) + "&suorgadminid=" +
                HttpUtility.UrlEncode(userId) + "&targetURL=" +
                HttpUtility.UrlEncode(url);
        }

        public string GetLoginUrl(object id)
        {
            if (Uri.TryCreate(Convert.ToString(id), UriKind.Absolute, out var url))
            {
                return url.GetLeftPart(UriPartial.Authority);
            }
            return "https://login.salesforce.com";
        }

        public string GetUrlViaAccessToken(string instanceUrl, string accessToken, string targetUrl)
        {
            if (string.IsNullOrEmpty(targetUrl)) return "";
            var url = instanceUrl + "/secur/frontdoor.jsp?sid=" +
                HttpUtility.UrlEncode(accessToken) +
                "&retURL=" +
                HttpUtility.UrlEncode(targetUrl.Replace(instanceUrl, ""));
            return url;
        }

        public bool IsHtmlResponse(HttpResponseMessage response)
        {
            return response.Content.Headers.TryGetValues("Content-Type", out var contentTypes) &&
                contentTypes.Any(t => t.StartsWith("text/html"));
        }

        public async Task<string> GetLoginWaitForRedirect(HttpClient httpClient, string instanceUrl, string url, string targetUrl)
        {
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GoOn();

            if (!IsHtmlResponse(response))
            {
                throw new InvalidOperationException();
            }

            var htmlContent = await response.Content.ReadAsStringAsync().GoOn();
            var expectedUrl = targetUrl.StartsWith(instanceUrl) ? targetUrl.Substring(instanceUrl.Length) : targetUrl;

            if (!htmlContent.Contains(expectedUrl))
            {
                throw new InvalidOperationException();
            }

            var nextResponse = await httpClient.GetAsync(targetUrl).GoOn();

            if (IsHtmlResponse(nextResponse))
            {
                var reg = new Regex(@"top\.window\.location\s*=\s*'([^']+)'");

                htmlContent = await nextResponse.Content.ReadAsStringAsync().GoOn();
                var re = reg.Match(htmlContent);

                while (re.Success)
                {
                    var regUrl = re.Groups[1].Value;
                    if (regUrl.StartsWith('/'))
                    {
                        regUrl = instanceUrl + regUrl;
                    }
                    nextResponse = await httpClient.GetAsync(regUrl).GoOn();
                    if (!IsHtmlResponse(nextResponse)) break;
                    htmlContent = await response.Content.ReadAsStringAsync().GoOn();
                    re = reg.Match(htmlContent);
                }
            }
            return htmlContent;
        }

        public async Task<byte[]> GetBytesViaAccessTokenAsync(string instanceUrl, string accessToken, string targetUrl)//, string mediaType)
        {
            if (string.IsNullOrEmpty(targetUrl)) return new byte[0];

            return await RunClientAsync(async (httpClient, cookieContainer, htmlContent) =>
            {
                var bytes = await httpClient.GetByteArrayAsync(targetUrl).GoOn();
                return bytes;
                //return string.Join("", "data:", mediaType, ";base64," + Convert.ToBase64String(bytes));
            }, instanceUrl, accessToken, targetUrl).GoOn();
        }

        public string DefaultDirectory
        {
            get
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
                    AppDomain.CurrentDomain.FriendlyName);
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public string GetContentType(string path)
        {
            var resExt = Path.GetExtension(path)?.ToLower();
            switch (resExt)
            {
                case ".css":
                    return "text/css";
                case ".eot":
                    return "application/vnd.ms-fontobject";
                case ".ico":
                    return "image/x-icon";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".js":
                    return "text/javascript";
                case ".otf":
                    return "application/font-otf";
                case ".png":
                    return "image/png";
                case ".svg":
                case ".svgz":
                    return "image/svg+xml";
                case ".tpl":
                    return "text/x-template";
                case ".ttf":
                    return "application/x-font-ttf";
                case ".woff":
                    return "font/woff";
                case ".woff2":
                    return "font/woff2";
            }
            return null;
        }

        public Stream GetResourceStream(string resPath)
        {
            if (string.IsNullOrEmpty(resPath)) return null;
            var asm = typeof(ResourceManager).Assembly;
            return asm.GetManifestResourceStream(string.Join("",
                "SF_DataExport.res.", resPath?.Replace("-sprite/", "_sprite/").Replace('/', '.')
            ));
        }


        public string GetDisplaySize(long fileSize)
        {
            var sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (fileSize >= 1024 && order < sizes.Length - 1)
            {
                order++;
                fileSize = fileSize / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            var result = String.Format("{0:0.##} {1}", fileSize, sizes[order]);
            return result;
        }
    }
}
