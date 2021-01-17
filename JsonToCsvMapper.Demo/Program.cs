using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace JsonToCsvMapper.Demo
{
    class Program
    {
        static readonly Logger log = new(Config.Storage.LogFilesPath);
        static readonly StringBuilder reportMissing = new();
        static int missingInOneProductCount;

        static void Main()
        {
            if (!Directory.Exists(Config.Storage.LogFilesPath))
            {
                Directory.CreateDirectory(Config.Storage.LogFilesPath);
            }
            log.WriteLine("• Initializing...");

            bool isSuccess = true;
            try
            {
                GenerateFiles();
            }
            catch (Exception ex)
            {
                isSuccess = false;
                log.WriteError(ex);
            }

            if (isSuccess)
            {
                log.WriteLine("• Success!");
            }
            else
            {
                log.WriteLine("• Fail =(");

                log.WriteLine("• Sending email...");
                try
                {
                    if (string.IsNullOrEmpty(Config.Settings.EmailOnFail))
                    {
                        log.WriteLine("• Skipped (email not provided)");
                    }
                    else
                    {
                        // Notify about failure here
                        log.WriteLine("• DONE");
                    }
                }
                catch (Exception ex)
                {
                    log.WriteError(ex);
                }
            }
            log.WriteLine("----------------------------------------------------");
        }

        private static void GenerateFiles()
        {
            log.WriteLine("• Checking output folder...");
            string path = GetOutputFolder();

            log.WriteLine("• Generating files...");
            Mapper mainMapper = new(GetCatalogfileMapping(), Path.Combine(path, "Catalog.txt"));
            List<Mapper> mappers = new()
            {
                new(GetAttributesMapping(), Path.Combine(path, "Attributes.txt")),
                new(GetMediaLinksMapping(), Path.Combine(path, "MediaLinks.txt"), printHeaders: false)
            };
            foreach (Mapper mapper in mappers)
            {
                mapper.PropertyMissing += Mapper_PropertyMissing;
                if (Config.Settings.LogMissingProperties)
                {
                    mapper.AnyPropertyMissing += Mapper_EntityAllOptionalPropertesMissing;
                }
            }

            log.WriteLine("• Sending requests and generating file contents...");
            foreach (JObject jsonProduct in GetDataFromAPI())
            {
                missingInOneProductCount = 0;

                bool lineSkipped = mainMapper.AppendEntity(jsonProduct);
                if (lineSkipped)
                {
                    // If we skip line in Catalog file there is no need to write info about this product into other files
                    continue;
                }

                foreach (Mapper mapper in mappers)
                {
                    mapper.AppendEntity(jsonProduct);
                }

                if (Config.Settings.LogMissingProperties && missingInOneProductCount > 0)
                {
                    log.WriteLine("({0} missing)", missingInOneProductCount);
                    log.WriteLine("--------------------------");
                }
            }
            mainMapper.Dispose();
            foreach (Mapper mapper in mappers)
            {
                mapper.Dispose();
            }

            if (reportMissing.Length != 0)
            {
                log.WriteLine("• Reporting missing...");
                // Notify regarding all important missing properties: reportMissing.ToString()
                log.WriteLine("• DONE");
            }
        }

        private static void Mapper_EntityAllOptionalPropertesMissing(object sender, PropertyMissingEventArgs e)
        {
            log.WriteLine($"Property '{e.PropertyName}' does not exists");
            missingInOneProductCount++;
        }

        private static void Mapper_PropertyMissing(object sender, PropertyMissingEventArgs e)
        {
            reportMissing.Append("- Property '");
            reportMissing.Append(e.PropertyName);
            reportMissing.Append("' does not exists for product ");
            reportMissing.Append(e.Entity["PrimaryID"] ?? "<no identifier>");
            reportMissing.Append(Environment.NewLine);
        }

        private static string GetOutputFolder()
        {
            string path = Config.Storage.FilesPath;
            if (!Directory.Exists(path))
            {
                //throw new DirectoryNotFoundException($@"Error: directory ""{path}"" doesn`t exists.");
                Directory.CreateDirectory(path);
            }
            return path;
        }

        #region Get Data From API
        private static IEnumerable<JObject> GetDataFromAPI()
        {
            string url = Config.Api.ApiUrl;
            
            string jsonResponse = GetMockResponse(url); // SendGetRequest(url)
            if (string.IsNullOrEmpty(jsonResponse))
            {
                throw new Exception("Server didn't return any data.");
            }
            JArray response = JArray.Parse(jsonResponse);

            foreach (JObject product in response)
            {
                yield return product;
            }

            GC.Collect();
        }

        static string GetMockResponse(string url)
        {
            return File.ReadAllText("mockApiResponse.json");
        }

        static async Task<string> SendGetRequest(string url)
        {
            HttpClient client = new()
            {
                Timeout = TimeSpan.FromMinutes(2),
                DefaultRequestHeaders = { { "Authorization", Config.Api.AuthorizationKey } }
            };

            string response = null;
            for (int i = 0; i < Config.Api.RetryCount; i++)
            {
                try
                {
                    response = await client.GetStringAsync(url);
                    break;
                }
                catch (Exception ex)
                {
                    log.WriteError(ex);
                }
            }
            return response;
        }
        #endregion

        private static OrderedDictionary GetCatalogfileMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "Product Code", MappingOptions.BuildMapping("PrimaryID", null, reportIfMissing: true) },
                { "Description", MappingOptions.BuildMapping("description") },
                { "Alias", MappingOptions.BuildConstant("") },
                { "TradePrice", MappingOptions.BuildMapping("tradePrice", "0") },
                { "SellPackWeight (kg)", MappingOptions.BuildMapping("sellPackWeightG", "0", (item) => (int.Parse(item) / 1000d).ToString()) },
                { "Country", MappingOptions.BuildMapping("countryOfOrigin") }
            };
            return mapping;
        }

        private static OrderedDictionary GetAttributesMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductID", MappingOptions.BuildMapping("PrimaryID") },
                { "Name", MappingOptions.BuildConstantList(new List<string>()
                    {
                        "Type", "Manufacturer", "Color", "Size", "On Promotion", "Dimensions", "Weight", "Eco-Aware", "Warranty",
                        "Recycled Percentage", "Recyclable", "Biodegradable", "Re-fillable",
                    }) 
                },
                { "Value", MappingOptions.BuildMappingList(new List<string>()
                    {
                        "Type", "Manufacturer", "Color", "Size", "On Promotion", "Dimensions", "Weight", "Eco-Aware", "Warranty",
                        "Recycled Percentage", "Recyclable", "Biodegradable", "Re-fillable",
                    }, null) 
                },
                { "Sequence", MappingOptions.BuildConstant("1") }, // For sorting
            };
            return mapping;
        }

        private static OrderedDictionary GetMediaLinksMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductId", MappingOptions.BuildMapping("PrimaryID") },
                { "MediaType", MappingOptions.BuildMappingList(
                    new List<string> { "imagesType", "datasheetType" },
                    action: type => 
                    {
                        type = type.ToUpper();
                        if (new string[] { "JPEG", "JPG", "PNG", "GIF" }.Contains(type))
                        {
                            type = "IMG";
                        }
                        return type;
                    }, 
                    arrayItemIndex: null) 
                },
                { "Order", MappingOptions.BuildMappingList(new List<string> 
                    { 
                        "imageSortOrder", "datasheetSortOrder"
                    }, arrayItemIndex: null) 
                },
                { "Url", MappingOptions.BuildMappingList(new List<string> 
                    { 
                        "imageURL", "datasheetURL"
                    }, 
                    defaultValue: null, 
                    arrayItemIndex: null) 
                },
            };
            return mapping;
        }
    }
}
