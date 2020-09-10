using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NServiceBus.Persistence.CosmosDB
{
    class CosmosDBContractResolver : DefaultContractResolver
    {
        protected override string ResolvePropertyName(string propertyName) => propertyName.Equals("Id") ? "id" : base.ResolvePropertyName(propertyName);

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (property.PropertyName.Equals("Id", StringComparison.Ordinal))
            {
                property.PropertyName = "id";
            }

            return property;
        }
    }
}
