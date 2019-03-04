using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.IO;
using System.Threading.Tasks;

namespace SF_DataExport
{
    public class JsonConfig
    {
        string JsonPath { get; set; }

        public JsonConfig(string jsonPath)
        {
            JsonPath = jsonPath;
        }

        public string GetPath() => JsonPath;

        public void SetPath(string jsonPath) => JsonPath = jsonPath;

        public JObject Read()
        {
            var json = new JObject();
            try
            {
                using (var stream = new FileStream(JsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        json = JsonConvert.DeserializeObject<JObject>(reader.ReadToEnd());
                    }
                }
            }
            catch { }
            return json;
        }

        public string[] List()
        {
            return Read().Properties().Select(p => p.Name).ToArray();
        }

        public T Get<T>(Func<JObject, T> getter)
        {
            return getter(Read());
        }

        public Task SaveAysnc()
        {
            return SaveAysnc(obj => { });
        }

        public async Task SaveAysnc(Action<JObject> setter)
        {
            var json = Read();
            setter(json);
            await File.WriteAllTextAsync(JsonPath, json.ToString());
        }
    }
}
