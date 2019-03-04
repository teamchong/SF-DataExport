using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SF_DataExport
{
    class ResourceHelper
    {
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
            }
            return null;
        }

        public Stream GetResourceStream(string resPath)
        {
            var asm = typeof(ResourceHelper).Assembly;
            return asm.GetManifestResourceStream($"SF_DataExport.{resPath?.Replace('/', '.').Replace("-sprite.", "_sprite.")}");
        }
    }
}
