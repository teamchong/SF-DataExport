using CsvHelper;
using CsvHelper.Configuration;
using DotNetForce;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SF_DataExport.Reducers
{
    public class GetLimits : IDispatcher
    {
        AppStore Store { get; }
        ResourceManager Resource { get; }
        JsonConfig OrgSettings { get; }

        public GetLimits(AppStore store, ResourceManager resource, OrgSettingsConfig orgSettings)
        {
            Store = store;
            Resource = resource;
            OrgSettings = orgSettings;
        }

        public async Task<JToken> DispatchAsync(JToken payload)
        {
            var instanceUrl = (string)payload?["instanceUrl"] ?? "";
            var showLimitsModal = !string.IsNullOrEmpty(instanceUrl);
            Store.Commit(new JObject { ["showLimitsModal"] = showLimitsModal });
            if (showLimitsModal)
            {
                var orgName = Resource.GetOrgName(instanceUrl);

                if (!string.IsNullOrEmpty(orgName))
                {
                    var accessToken = (string)OrgSettings.Get(o => o[instanceUrl]?[OAuth.ACCESS_TOKEN]);
                    var client = new DNFClient(instanceUrl, accessToken);

                    var request = new BatchRequest();
                    request.Limits();
                    request.Query("SELECT Name,UsedLicenses,TotalLicenses FROM UserLicense WHERE Status = 'Active' AND TotalLicenses > 0 ORDER BY Name");
                    var result = await client.Composite.BatchAsync(request);

                    var orgLimits = new JArray();

                    var limits = (JObject)result.Results("0") ?? new JObject();
                    foreach (var limitsProp in limits.Properties())
                    {
                        var limit = (JObject)limitsProp.Value;
                        orgLimits.Add(new JObject
                        {
                            ["Name"] = limitsProp.Name,
                            ["Remaining"] = limit["Remaining"],
                            ["Max"] = limit["Max"],
                        });
                    }
                    foreach (var userLicense in client.GetEnumerable(result.Queries("1")))
                    {
                        orgLimits.Add(new JObject
                        {
                            ["Name"] = userLicense["Name"],
                            ["Remaining"] = (double)userLicense["TotalLicenses"] - (double)userLicense["UsedLicenses"],
                            ["Max"] = userLicense["TotalLicenses"],
                        });
                    }

                    var orgLimitsTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                    Store.Commit(new JObject
                    {
                        ["orgLimits"] = orgLimits,
                    });

                    var header = string.Join('\t', "Name", "Remaining", "Max", "Time");
                    var exists = false;
                    var file = new FileInfo(Path.Combine(Resource.DefaultDirectory, orgName + ".limits.csv"));
                    if (file.Exists)
                    {
                        using (var fileStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (var streamReader = new StreamReader(fileStream, true))
                            {
                                if (await streamReader.ReadLineAsync().GoOn() == header)
                                {
                                    exists = true;
                                }
                            }
                        }
                        if (!exists)
                        {
                            file.Delete();
                        }
                    }
                    var csvConfig = new Configuration { Delimiter = "\t", Encoding = Encoding.Unicode };
                    using (var fileStream = file.Open(exists ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        using (var streamWriter = new StreamWriter(fileStream, csvConfig.Encoding))
                        {
                            using (var writer = new CsvWriter(streamWriter, csvConfig))
                            {
                                if (!exists)
                                {
                                    writer.WriteField("Name");
                                    writer.WriteField("Remaining");
                                    writer.WriteField("Max");
                                    writer.WriteField("Time");
                                    await writer.NextRecordAsync().GoOn();
                                    await writer.FlushAsync().GoOn();
                                }

                                for (var i = 0; i < orgLimits.Count; i++)
                                {
                                    var orgLimit = orgLimits[i];
                                    writer.WriteField((string)orgLimit["Name"]);
                                    writer.WriteField((double)orgLimit["Remaining"]);
                                    writer.WriteField((double)orgLimit["Max"]);
                                    writer.WriteField(orgLimitsTime);
                                    await writer.NextRecordAsync().GoOn();
                                    await writer.FlushAsync().GoOn();
                                }
                            }
                        }
                    }

                    Store.Commit(new JObject
                    {
                        ["orgLimitsLog"] = await Resource.GetOrgLimitsLogAsync(orgName)
                    });
                }
            }
            return null;
        }
    }
}