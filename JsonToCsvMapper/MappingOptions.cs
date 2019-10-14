using System;
using System.Collections.Generic;

namespace JsonToCsvMapper
{
    public class MappingOptions
    {
        /// <summary>
        /// Json property names to map to single output column
        /// </summary>
        public IReadOnlyList<string> MappingHeaders { get; private set; }
        /// <summary>
        /// Constant values to map to single output column
        /// </summary>
        public IReadOnlyList<string> ConstValues { get; private set; }
        /// <summary>
        /// Index of the required element in case value is an array.
        /// <para/><b>null</b> = all
        /// </summary>
        public int? ArrayItemIndex { get; private set; }
        /// <summary>
        /// Action that is performed on property value 
        /// before writing it to output
        /// </summary>
        public Func<string, string> Action { get; private set; }
        /// <summary>
        /// Placeholder in case if value is not found.
        /// <para/><b>null</b> = skip whole entity if value not found
        /// </summary>
        public string DefaultValue { get; private set; }
        /// <summary>
        /// Fire <b>PropertyMissing</b> event if this property 
        /// is not present in provided entity
        /// </summary>
        public bool ReportIfMissing { get; private set; }

        private MappingOptions(List<string> mappingHeaders, int? arrayItemIndex, string defaultValue, Func<string, string> action, bool reportIfMissing)
        {
            MappingHeaders = mappingHeaders;
            ArrayItemIndex = arrayItemIndex;
            DefaultValue = defaultValue;
            Action = action;
            ReportIfMissing = reportIfMissing;
        }

        private MappingOptions()
        { }

        public static MappingOptions BuildConstant(string value)
        {
            return new MappingOptions()
            {
                ConstValues = new List<string> { value }
            };
        }

        public static MappingOptions BuildMapping(string mappingHeader, string defaultValue = "", Func<string, string> action = null, int? arrayItemIndex = 0, bool reportIfMissing = false)
        {
            return new MappingOptions(new List<string> { mappingHeader }, arrayItemIndex, defaultValue, action, reportIfMissing);
        }

        public static MappingOptions BuildMappingList(List<string> mappingHeaders, string defaultValue = "", Func<string, string> action = null, int? arrayItemIndex = 0, bool reportIfMissing = false)
        {
            if (mappingHeaders.Count == 0)
            {
                throw new InvalidOperationException("You should specify at least one mapping header.");
            }

            return new MappingOptions(mappingHeaders, arrayItemIndex, defaultValue, action, reportIfMissing);
        }

        public static object BuildConstantList(List<string> constantValues)
        {
            if (constantValues.Count == 0)
            {
                throw new InvalidOperationException("You should specify at least one constant values.");
            }

            return new MappingOptions()
            {
                ConstValues = constantValues
            };
        }
    }
}