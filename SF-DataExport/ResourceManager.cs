using DotNetForce;
using PuppeteerSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SF_DataExport
{
    public class ResourceManager
    {

        public string CONTENT_HTML_START { get; protected set; }

        public string CONTENT_HTML_END { get; protected set; }

        public ResourceManager()
        {
            var asm = typeof(ResourceManager).Assembly;
            var resList = asm.GetManifestResourceNames();
            CONTENT_HTML_START = string.Join("", @"<html>
<head>
<title>Salesforce DataExport</title>
<link rel='shortcut icon' type='image/x-icon' href='/favicon.ico'>
<link rel='stylesheet' type='text/css' href='/material-icons.css'>
<link rel='stylesheet' type='text/css' href='/vuetify.css'>
<link rel='stylesheet' type='text/css' href='/slds.css'>
<style>[v-cloak]{display:none;}</style>
<script src='/vue.js'></script>
<script src='/vuex.js'></script>
<script src='/vuetify.js'></script>
<script src='/rxjs.js'></script>
</head>
<body>
<script>const appState=");
            CONTENT_HTML_END = string.Join("", @"
</script>", GetResource("app.html"), @"
</body>
</html>");
        }

        public string Execute(string exeFileName)
        {
            var process = new Process();
            var outputStringBuilder = new StringBuilder();

            try
            {
                process.StartInfo.FileName = exeFileName;
                process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
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

        public void OpenBrowserIncognito(string url, string chromePath)
        {
            var process = new ProcessStartInfo(chromePath, "-incognito " + url);
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

        public async Task<string> WaitForRedirectAsync(HttpClient client, string instanceUrl, string htmlContent, string targetUrl)
        {
            var expectedUrl = targetUrl.StartsWith(instanceUrl) ? targetUrl.Substring(instanceUrl.Length) : targetUrl;
            if (!htmlContent.Contains(expectedUrl))
            {
                throw new InvalidOperationException();
            }

            htmlContent = await client.GetStringAsync(targetUrl).GoOn();
            var reg = new Regex(@"top\.window\.location\s*=\s*'([^']+)'");
            var re = reg.Match(htmlContent);

            while (re.Success)
            {
                var url = re.Groups[1].Value;
                if (url.StartsWith('/'))
                {
                    url = instanceUrl + url;
                }
                htmlContent = await client.GetStringAsync(url).GoOn();
                re = reg.Match(htmlContent);
            }
            return htmlContent;
        }

        public async Task<string> GetDataUriViaAccessToken(string instanceUrl, string accessToken, string targetUrl, string mediaType)
        {
            var url = GetUrlViaAccessToken(instanceUrl, accessToken, targetUrl);
            if (string.IsNullOrEmpty(url)) return "";

            var cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            {
                using (var client = new HttpClient(handler))
                {
                    var htmlContent = await client.GetStringAsync(url).GoOn();
                    var bytes = await client.GetByteArrayAsync(targetUrl).GoOn();
                    return string.Join("", "data:", mediaType, ";base64," + Convert.ToBase64String(bytes));
                }
            }
        }

        public string GetContentType(string path)
        {
            var resExt = Path.GetExtension(path)?.ToLower();
            switch (resExt)
            {
                case ".svg":
                case ".svgz":
                    return "image/svg+xml";
                case ".woff":
                    return "font/woff";
                case ".woff2":
                    return "font/woff2";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".ttf":
                    return "application/x-font-ttf";
                case ".css":
                    return "text/css";
                case ".js":
                    return "text/javascript";
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
