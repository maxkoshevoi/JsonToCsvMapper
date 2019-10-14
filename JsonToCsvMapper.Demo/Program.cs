using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace JsonToCsvMapper.Demo
{
    class Program
    {
        static Logger log;
        static StringBuilder reportMissing;
        static int missingInOneProductCount;

        static void Main()
        {
            if (!Directory.Exists(Config.Storage.LogFilesPath))
            {
                Directory.CreateDirectory(Config.Storage.LogFilesPath);
            }
            reportMissing = new StringBuilder();
            log = new Logger(Config.Storage.LogFilesPath);
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
            string catalogfilePath = Path.Combine(path, Config.Ftp.CatalogFile.Path);
            string txtFolder = Path.Combine(Config.Ftp.Data.PathComplete, Config.Ftp.Data.FileNameComplete.Remove(Config.Ftp.Data.FileNameComplete.LastIndexOf('.')));
            string txtPath = Path.Combine(path, txtFolder);
            string mediaFolder = Path.Combine(Config.Ftp.Media.PathComplete, Config.Ftp.Media.FileNameComplete.Remove(Config.Ftp.Media.FileNameComplete.LastIndexOf('.')));
            string mediaPath = Path.Combine(path, mediaFolder);

            log.WriteLine("• Creating directories if needed...");
            Directory.CreateDirectory(catalogfilePath);
            Directory.CreateDirectory(txtPath);
            Directory.CreateDirectory(mediaPath);

            log.WriteLine("• Generating files...");
            Mapper mainMapper = new Mapper(GetCatalogfileMapping(), Path.Combine(catalogfilePath, "Catalog.txt"));
            List<Mapper> mappers = new List<Mapper>
            {
                new Mapper(GetCompAttributesMapping(), Path.Combine(txtPath, "CompAttributes.txt")),
                new Mapper(GetFeaturesMapping(), Path.Combine(txtPath, "Features.txt")),
                new Mapper(GetOptionsMapping(), Path.Combine(txtPath, "Options.txt")),
                new Mapper(GetProductsMapping(), Path.Combine(txtPath, "Products.txt")),
                new Mapper(GetAttributesMapping(), Path.Combine(txtPath, "Attributes.txt")),
                new Mapper(GetMediaLinksMapping(), Path.Combine(mediaPath, "MediaLinks.txt"), printHeaders: false)
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
            string path = Config.Storage.TempFilesPath;
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
            JObject response;
            do
            {
                string jsonResponse = GetMockResoponce(url);
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    throw new Exception("Server didn't return any data.");
                }
                response = JObject.Parse(jsonResponse);

                url = response["next"].Value<string>();
                foreach (JObject product in response["data"])
                {
                    yield return product;
                }

                GC.Collect();

                log.WriteLine("{0} retrieved, {1} left", response["count"], response["total"].Value<int>() - response["count"].Value<int>());

            } while (response["count"].Value<int>() < response["total"].Value<int>());
        }

        static string GetMockResoponce(string url)
        {
            // TODO
            return "";
        }

        static string SendGetRequest(string url)
        {
            string response = null;
            for (int i = 0; i < Config.Api.RetryCount; i++)
            {
                try
                {
                    WebRequest webRequest = WebRequest.Create(url);
                    webRequest.Method = "GET";
                    webRequest.Timeout = 120000;
                    webRequest.ContentType = "application/json";
                    webRequest.Headers.Add("Authorization", Config.Api.AuthorizationKey);

                    using (Stream s = webRequest.GetResponse().GetResponseStream())
                    using (StreamReader sr = new StreamReader(s))
                    {
                        response = sr.ReadToEnd();
                    }
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
                { "Prod Code", MappingOptions.BuildMapping("PrimaryID", null, reportIfMissing: true) },
                { "ORProductID", MappingOptions.BuildMapping("PrimaryID") },
                { "Description", MappingOptions.BuildMapping("description") },
                { "VATCode", MappingOptions.BuildMapping("VATCode") },
                { "AltCode", MappingOptions.BuildMapping("altCode") },
                { "Range", MappingOptions.BuildMapping("range", "0") },
                { "RangeGroup", MappingOptions.BuildMapping("rangeGroup", "0") },
                { "RRP", MappingOptions.BuildMapping("RRP", "0") },
                { "TradePrice", MappingOptions.BuildMapping("tradePrice", "0") },
                { "Per", MappingOptions.BuildMapping("per", null, reportIfMissing: true) },
                { "Pack", MappingOptions.BuildMapping("pack", "0") },
                { "Reserved", MappingOptions.BuildMapping("reserved") },
                { "JHProdCode", MappingOptions.BuildMapping("jhProdCode") },
                { "KingfieldProdCode", MappingOptions.BuildMapping("kingfieldProdCode") },
                { "SupRef", MappingOptions.BuildMapping("supRef") },
                { "PriceChange", MappingOptions.BuildMapping("priceChange", "0") },
                { "SellPackChange", MappingOptions.BuildMapping("sellPackChange", "0") },
                { "DescriptionChange", MappingOptions.BuildMapping("descriptionChange", "0") },
                { "NewProduct", MappingOptions.BuildMapping("newProduct", "0") },
                { "Discontinued", MappingOptions.BuildMapping("discontinued", "0") },
                { "DiscountException", MappingOptions.BuildMapping("discountException", "0") },
                { "JulyCatPage", MappingOptions.BuildMapping("JulyCatPage") },
                { "JulCatItem", MappingOptions.BuildMapping("JulCatItem") },
                { "JanCatPage", MappingOptions.BuildMapping("janCatPage", "0") },
                { "JanCatAlpha", MappingOptions.BuildMapping("janCatAlpha") },
                { "MiniCatPage", MappingOptions.BuildMapping("miniCatPage") },
                { "MiniCatAlpha", MappingOptions.BuildMapping("miniCatAlpha") },
                { "FurnCatPage", MappingOptions.BuildMapping("furnCatPage") },
                { "FurnCatItem", MappingOptions.BuildMapping("furnCatItem") },
                { "PricingLetter", MappingOptions.BuildMapping("matrixBandPricingLetter") },
                { "EAN", MappingOptions.BuildMapping("EAN") },
                { "BossCode", MappingOptions.BuildMapping("bossCode") },
                { "CatSpecial", MappingOptions.BuildMapping("catSpecial", "0") },
                { "ECCommodityCode", MappingOptions.BuildMapping("ECCommodityCode") },
                { "PreviousSepCatPage", MappingOptions.BuildMapping("prevSetCatPage") },
                { "PreviousSepCatItem", MappingOptions.BuildMapping("prevSepCatItem") },
                { "FiveStarProduct", MappingOptions.BuildMapping("fiveStarProduct", "0") },
                { "GroupTable", MappingOptions.BuildMapping("GroupTable") },
                { "NonReturnable", MappingOptions.BuildMapping("nonReturnable", "0") },
                { "EnvironmentalCode", MappingOptions.BuildMapping("environmentalCode") },
                { "Brand", MappingOptions.BuildMapping("brand") },
                { "CodeColour", MappingOptions.BuildMapping("codeColour") }, // No defaut value! (Magenta?)
                { "Description105", MappingOptions.BuildMapping("description105") },
                { "SellPackWeight (kg)", MappingOptions.BuildMapping("sellPackWeightG", "0", (item) => (int.Parse(item) / 1000d).ToString()) },
                { "ConsumerDeliveryExpectation", MappingOptions.BuildMapping("consumerDeliveryExpectation") },
                { "NumberinPack", MappingOptions.BuildMapping("numberInPack") },
                { "CountryOfOrigin", MappingOptions.BuildMapping("countryOfOrigin") },
                { "PromoCode", MappingOptions.BuildMapping("promoCode") }
            };
            return mapping;
        }

        private static OrderedDictionary GetCompAttributesMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductID", MappingOptions.BuildMapping("PrimaryID") },
                { "Name", MappingOptions.BuildConstantList(new List<string>()
                    {
                        "Type", "Manufacturer", "Colour", "Size", "On Promotion", "Dimensions", "Weight", "Eco-Aware", "Warranty",
                        "Recycled Percentage", "Recyclable", "Biodegradable", "Re-fillable", "Waste Collection Available", "Totally Chlorine Free",
                        "Elemental ChLorine Free", "EMAS Accredited", "FSC® Certified", "FSC® Certification No", "PEFC Accredited", "PEFC Accreditation No",
                        "Nordic Swan Accredited", "IEMA Acorn Scheme", "Paper by Nature", "NAPM Accredited", "ROHS Accredited", "EU Ecolabel Accredited",
                        "Blue Angel Accredited", "WWF Member", "Rainforest Alliance", "Fairtrade", "EN Standards Accredited", "EN Standard Code",
                        "WEEE Compliant", "Other Enviro Details", "Top Material (13030605)", "Form (13030606)", "Leg Type(13030607)", "Orientation (13030608)",
                        "Pedestals (13030609)", "Cable Ports (13030610)", "Cable Management (13030611)", "Frame Colour (13030613)", "Top Thickness (13030631)",
                        "Floor Levellers (13030632)", "No. of Pedestals (13030633)", "Flat Packed (13030634)", "Marketing Text",
                    }) },
                { "Value", MappingOptions.BuildMappingList(new List<string>()
                    {
                        "Type", "Manufacturer", "Colour", "Size", "On Promotion", "Dimensions", "Weight", "Eco-Aware", "Warranty",
                        "Recycled Percentage", "Recyclable", "Biodegradable", "Re-fillable", "Waste Collection Available", "Totally Chlorine Free",
                        "Elemental ChLorine Free", "EMAS Accredited", "FSC® Certified", "FSC® Certification No", "PEFC Accredited", "PEFC Accreditation No",
                        "Nordic Swan Accredited", "IEMA Acorn Scheme", "Paper by Nature", "NAPM Accredited", "ROHS Accredited", "EU Ecolabel Accredited",
                        "Blue Angel Accredited", "WWF Member", "Rainforest Alliance", "Fairtrade", "EN Standards Accredited", "EN Standard Code",
                        "WEEE Compliant", "Other Enviro Details", "Top Material (13030605)", "Form (13030606)", "Leg Type(13030607)", "Orientation (13030608)",
                        "Pedestals (13030609)", "Cable Ports (13030610)", "Cable Management (13030611)", "Frame Colour (13030613)", "Top Thickness (13030631)",
                        "Floor Levellers (13030632)", "No. of Pedestals (13030633)", "Flat Packed (13030634)", "marketingText",
                    }, null) },
                { "Group", MappingOptions.BuildConstant("") },
                { "Sequence", MappingOptions.BuildConstant("1") },
                { "Level", MappingOptions.BuildConstant("0") },
                { "Overview", MappingOptions.BuildConstant("1") },
            };
            return mapping;
        }

        private static OrderedDictionary GetFeaturesMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductID", MappingOptions.BuildMapping("PrimaryID") },
                { "Description", MappingOptions.BuildMappingList(new List<string>
                    {
                        "bullet01", "bullet02", "bullet03", "bullet04", "bullet05", "bullet06", "bullet07", "bullet08", "bullet09",
                        "bullet10", "bullet11",  "bullet12", "bullet13", "bullet14", "bullet15", "bullet16", "bullet17", "bullet18", "bullet19",
                        "bullet20", "bullet21", "bullet22", "bullet23", "bullet24", "bullet25", "bullet26", "bullet27", "bullet28", "bullet29",
                        "bullet30", "bullet31", "bullet32", "bullet33", "bullet34", "bullet35",
                    }, null) },
                { "Sequence", MappingOptions.BuildConstant("1") },
            };
            return mapping;
        }

        private static OrderedDictionary GetOptionsMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductID", MappingOptions.BuildMapping("PrimaryID") },
                { "PartNo", MappingOptions.BuildMapping("Non-existing property", null, reportIfMissing: true) },
                { "OptionID", MappingOptions.BuildMapping("Non-existing property", null) },
            };
            return mapping;
        }

        private static OrderedDictionary GetProductsMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductID", MappingOptions.BuildMapping("PrimaryID") },
                { "Manufacturer", MappingOptions.BuildMapping("brand") },
                { "PartNo", MappingOptions.BuildMapping("supRef") },
                { "Model", MappingOptions.BuildMapping("description105") },
                { "AliasCode", MappingOptions.BuildMapping("advantiaCode") }
            };
            return mapping;
        }

        private static OrderedDictionary GetAttributesMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductID", MappingOptions.BuildMapping("PrimaryID") },
                { "Name", MappingOptions.BuildConstantList(new List<string>
                    {
                        "Internal Number", "ISPC Code", "Marketing"
                    }) },
                { "Value", MappingOptions.BuildMappingList(new List<string>
                    {
                        "PrimaryID", "bossCode", "marketingText"
                    }) },
            };
            return mapping;
        }

        private static OrderedDictionary GetMediaLinksMapping()
        {
            OrderedDictionary mapping = new OrderedDictionary()
            {
                { "ProductId", MappingOptions.BuildMapping("PrimaryID") },
                { "MediaType", MappingOptions.BuildMappingList(
                    new List<string> { "imagesType", "datasheetType", "coshhFileType" },
                    action: (value) => {
                        value = value.ToUpper();
                        if (new string[] { "JPEG", "JPG", "PNG", "GIF" }.Contains(value))
                        {
                            value = "IMG";
                        }
                        return value;
                    }, arrayItemIndex: null) },
                { "Placeholder2", MappingOptions.BuildConstant("") },
                { "Order", MappingOptions.BuildMappingList(new List<string> { "imageSortOrder", "datasheetSortOrder", "coshhSortOrder" }, arrayItemIndex: null) },
                { "Placeholder4", MappingOptions.BuildConstant("") },
                { "Description", MappingOptions.BuildConstant("") },
                { "Placeholder6", MappingOptions.BuildConstant("") },
                { "Placeholder7", MappingOptions.BuildConstant("") },
                { "Placeholder8", MappingOptions.BuildConstant("") },
                { "Placeholder9", MappingOptions.BuildConstant("") },
                { "Url", MappingOptions.BuildMappingList(new List<string> { "imageURL", "datasheetURL", "coshhURL" }, null, arrayItemIndex: null) },
            };
            return mapping;
        }
    }
}
