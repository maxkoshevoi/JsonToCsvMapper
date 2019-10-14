using System;
using Newtonsoft.Json.Linq;

namespace JsonToCsvMapper
{
    public class PropertyMissingEventArgs : EventArgs
    {
        public readonly string PropertyName;
        public readonly MappingOptions Mapping;
        public readonly JObject Entity;

        public PropertyMissingEventArgs(string name, MappingOptions mapping, JObject entity)
        {
            PropertyName = name;
            Mapping = mapping;
            Entity = (JObject)entity.DeepClone();
        }
    }
}