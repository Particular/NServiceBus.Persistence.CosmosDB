namespace NServiceBus.Persistence.CosmosDB
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    class CosmosDBContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);
            foreach (var property in properties)
            {
                if (!property.PropertyName.Equals("Id", StringComparison.Ordinal))
                {
                    continue;
                }

                properties.Add(new JsonProperty
                {
                    PropertyName = "id",
                    UnderlyingName = "id",
                    PropertyType = property.PropertyType,
                    AttributeProvider = property.AttributeProvider,
                    ValueProvider = property.ValueProvider,
                    Readable = property.Readable,
                    Writable = property.Writable,
                    ItemIsReference = false,
                    TypeNameHandling = TypeNameHandling.None,
                });
                return properties;
            }

            return properties;
        }
    }
}