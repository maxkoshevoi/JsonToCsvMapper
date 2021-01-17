using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace JsonToCsvMapper
{
    public class Mapper : IDisposable
    {
        /// <summary>
        /// Fiers if property marked as ReportIfMissing is missing
        /// </summary>
        public event EventHandler<PropertyMissingEventArgs> PropertyMissing;
        /// <summary>
        /// Fiers is any property is missing
        /// </summary>
        public event EventHandler<PropertyMissingEventArgs> AnyPropertyMissing;

        private readonly OrderedDictionary mapping;
        private readonly StreamWriter outputStream;
        private readonly char fieldSeperator;

        public Mapper(OrderedDictionary mapping, string outputSFilePath, Encoding encoding = null, char fieldSeperator = '|', bool printHeaders = true) : 
            this(mapping, RecreateFile(outputSFilePath, encoding), fieldSeperator, printHeaders)
        { }

        public Mapper(OrderedDictionary mapping, StreamWriter outputStream, char fieldSeperator = '|', bool printHeaders = true)
        {
            if (mapping.Keys.OfType<string>().Count() != mapping.Keys.Count ||
                mapping.Values.OfType<MappingOptions>().Count() != mapping.Values.Count)
            {
                throw new ArgumentException($"{nameof(mapping)} Keys should be of type {typeof(string).Name} and Values of type {typeof(MappingOptions).Name}");
            }

            this.mapping = mapping;
            this.outputStream = outputStream;
            this.fieldSeperator = fieldSeperator;

            if (printHeaders)
            {
                PrintHeaders();
            }
        }

        public static StreamWriter RecreateFile(string filePath, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;

            File.Delete(filePath);
            File.Create(filePath).Close();

            return new(File.Open(filePath, FileMode.Append, FileAccess.Write), encoding);
        }

        /// <summary>
        /// Prints column names
        /// </summary>
        public void PrintHeaders()
        {
            string headerString = string.Join(fieldSeperator.ToString(), mapping.Keys.Cast<string>().ToArray());
            outputStream.WriteLine(headerString);
        }

        /// <summary>
        /// Maps entity via provided mapping and outputs result to provided StreamWriter
        /// </summary>
        /// <param name="entity">Input entity to map</param>
        /// <returns>Whether the line was skipped</returns>
        public bool AppendEntity(JObject entity)
        {
            bool skipLine = false;
            List<string> resultLines = new();
            List<MappingOptions> multipleMappingHeaders = new();
            List<List<List<string>>> multipleDataHeaders = new();
            StringBuilder line = new();
            foreach (string header in mapping.Keys)
            {
                string value;

                if (line.Length > 0)
                {
                    line.Append(fieldSeperator);
                }
                MappingOptions mappingOption = (MappingOptions)mapping[header];
                if (mappingOption.ConstValues != null)
                {
                    if (mappingOption.ConstValues.Count > 1)
                    {
                        value = "{h" + multipleMappingHeaders.Count + "}";
                        multipleMappingHeaders.Add(mappingOption);
                    }
                    else
                    {
                        value = mappingOption.ConstValues[0];
                    }
                }
                else if (mappingOption.MappingHeaders.Count > 1)
                {
                    value = "{h" + multipleMappingHeaders.Count + "}";
                    multipleMappingHeaders.Add(mappingOption);
                }
                else
                {
                    List<string> values = GetNonConstValue(entity, mappingOption, 0, out skipLine);
                    if (skipLine)
                    {
                        break;
                    }
                    if (values.Count <= 1)
                    {
                        value = values[0];
                    }
                    else
                    {
                        if (multipleDataHeaders.Count == 0)
                        {
                            multipleDataHeaders.Add(new());
                        }
                        value = "{d0_" + multipleDataHeaders[0].Count + "}";
                        multipleDataHeaders[0].Add(values);
                    }
                }
                line.Append(value);
            }


            if (!skipLine)
            {
                string strLine = line.ToString();
                List<string> linesToAdd = new();

                // Line is complete
                if (multipleMappingHeaders.Count == 0 && multipleDataHeaders.Count == 0)
                {
                    linesToAdd.Add(strLine);
                }

                // Line has list of headers/constants per column(s)
                if (multipleMappingHeaders.Count > 0)
                {
                    int len =
                        multipleMappingHeaders[0].MappingHeaders != null ?
                        multipleMappingHeaders[0].MappingHeaders.Count :
                        multipleMappingHeaders[0].ConstValues.Count;

                    for (int i = 0; i < len; i++)
                    {
                        string strNLine = strLine;
                        int multipleDataHeadersMax = multipleDataHeaders.Count;
                        for (int h = 0; h < multipleMappingHeaders.Count; h++)
                        {
                            MappingOptions mappingOption = multipleMappingHeaders[h];
                            if (mappingOption.ConstValues != null)
                            {
                                strNLine = strNLine.Replace("{h" + h + "}", mappingOption.ConstValues[i]);
                            }
                            else
                            {
                                List<string> values = GetNonConstValue(entity, mappingOption, i, out skipLine);
                                if (skipLine)
                                {
                                    break;
                                }
                                string value;
                                if (values.Count <= 1)
                                {
                                    value = values[0];
                                }
                                else
                                {
                                    if (multipleDataHeaders.Count == multipleDataHeadersMax)
                                    {
                                        multipleDataHeaders.Add(new());
                                    }
                                    value = "{d" + multipleDataHeadersMax + "_" + multipleDataHeaders[multipleDataHeadersMax].Count + "}";
                                    multipleDataHeaders[multipleDataHeadersMax].Add(values);
                                }
                                strNLine = strNLine.Replace("{h" + h + "}", value);
                            }
                        }
                        if (!skipLine)
                        {
                            linesToAdd.Add(strNLine);
                        }
                    }
                }

                // Line has multiple results per column
                if (multipleDataHeaders.Count > 0)
                {
                    List<string> linesWithDataToAdd = new();
                    foreach (string protoLine in linesToAdd)
                    {
                        bool isFound = false;
                        for (int i = 0; i < multipleDataHeaders.Count; i++)
                        {
                            if (protoLine.Contains("{d" + i + "_0}"))
                            {
                                for (int j = 0; j < multipleDataHeaders[i][0].Count; j++)
                                {
                                    string strNLine = protoLine;
                                    for (int d = 0; d < multipleDataHeaders[i].Count; d++)
                                    {
                                        strNLine = strNLine.Replace("{d" + i + "_" + d + "}", multipleDataHeaders[i][d][j]);
                                    }
                                    linesWithDataToAdd.Add(strNLine);
                                }
                                isFound = true;
                                break;
                            }
                        }
                        if (!isFound)
                        {
                            linesWithDataToAdd.Add(protoLine);
                        }
                    }
                    linesToAdd = linesWithDataToAdd;
                }

                resultLines.AddRange(linesToAdd);
            }

            foreach (string l in resultLines)
            {
                outputStream.WriteLine(l);
            }

            return skipLine;
        }

        private List<string> GetNonConstValue(JObject product, MappingOptions mappingOption, int headerIndex, out bool skipLine)
        {

            List<string> value = new();
            skipLine = false;
            JToken jsonValue = product[mappingOption.MappingHeaders[headerIndex]];

            if (jsonValue == null)
            {
                if (mappingOption.DefaultValue != null)
                {
                    AnyPropertyMissing?.Invoke(this, new PropertyMissingEventArgs(mappingOption.MappingHeaders[headerIndex], mappingOption, product));
                }
                if (mappingOption.ReportIfMissing)
                {
                    PropertyMissing?.Invoke(this, new PropertyMissingEventArgs(mappingOption.MappingHeaders[headerIndex], mappingOption, product));
                }

                value.Add(mappingOption.DefaultValue);
            }
            else if (jsonValue is JArray)
            {
                if (jsonValue.Count() <= mappingOption.ArrayItemIndex)
                {
                    value.Add(mappingOption.DefaultValue);
                }
                else if (mappingOption.ArrayItemIndex.HasValue)
                {
                    value.Add(jsonValue[mappingOption.ArrayItemIndex].ToString());
                }
                else
                {
                    value = jsonValue.ToObject<List<string>>();
                    if (value.Count == 0)
                    {
                        value.Add(mappingOption.DefaultValue);
                    }
                }
            }
            else
            {
                value.Add(jsonValue.ToString());
            }

            if (value[0] == null)
            {
                skipLine = true;
            }
            else
            {
                value = value.Select(v => v.Trim().Replace("\n", "").Replace("\r", "")).ToList();
                if (mappingOption.Action != null)
                {
                    value = value.Select(v => mappingOption.Action(v)).ToList();
                }
            }

            return value;
        }

        /// <summary>
        /// Closes StreamWriter
        /// </summary>
        public void Dispose()
        {
            outputStream.Close();
        }
    }
}
