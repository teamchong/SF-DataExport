using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SF_DataExport
{
    public class JsonConfig
    {
        JObject Data { get; set; }
        string JsonFilePath { get; set; }
        SemaphoreSlim Throttler { get; }

        public JsonConfig(string jsonFilePath)
        {
            JsonFilePath = jsonFilePath;
            Data = ReadFile();
            Throttler = new SemaphoreSlim(1, 1);
        }

        public string GetFilePath() => JsonFilePath;

        public string GetDirectoryPath() => Path.GetDirectoryName(JsonFilePath).TrimEnd(Path.DirectorySeparatorChar);

        public void SetPath(string jsonFilePath)
        {
            JsonFilePath = jsonFilePath;
            Data = ReadFile();
        }

        JObject ReadFile()
        {
            var json = new JObject();
            try
            {
                var fi = new FileInfo(JsonFilePath);
                if (fi.Exists)
                {
                    using (var stream = fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            json = JsonConvert.DeserializeObject<JObject>(reader.ReadToEnd()) ?? new JObject();
                        }
                    }
                }
            }
            catch { }
            return json;
        }

        public string[] List()
        {
            return Data.Properties().Select(p => p.Name).ToArray();
        }

        public T Get<T>(Func<JObject, T> getter)
        {
            return getter(Data);
        }

        public string GetString(string name)
        {
            return (string)Data[name] ?? "";
        }

        public Task SaveAysnc()
        {
            return SaveAysnc(obj => obj);
        }

        public Task SaveAysnc(Action<JObject> setter)
        {
            setter(Data);
            return SaveAysnc();
        }

        public async Task SaveAysnc(Func<JObject, JObject> setter)
        {
            await Throttler.WaitAsync().GoOn();

            try
            {
                Data = setter(Data);
                var fi = new FileInfo(JsonFilePath);
                using (var stream = fi.Open(FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        await writer.WriteAsync(Data.ToString()).GoOn();
                    }
                }
            }
            finally
            {
                Throttler.Release();
            }
        }
    }
}
