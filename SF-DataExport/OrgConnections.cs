using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.IO;
using System.Threading.Tasks;

namespace SF_DataExport
{
    public class OrgConnections
    {
        string JsonPath { get; set; }

        public OrgConnections(string jsonPath)
        {
            JsonPath = Path.Combine(jsonPath, "organization.json");
        }

        public void ChangePath(string jsonPath)
        {
            JsonPath = Path.Combine(jsonPath, "organization.json");
        }

        public JObject Read()
        {
            var organizations = new JObject();
            try
            {
                using (var stream = new FileStream(JsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        organizations = JsonConvert.DeserializeObject<JObject>(reader.ReadToEnd());
                    }
                }
            }
            catch { }
            return organizations;
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
