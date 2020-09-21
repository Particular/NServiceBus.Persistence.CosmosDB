namespace NServiceBus.Persistence.CosmosDB
{
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    class CosmosDBContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (member.Name == "Id")
            {
                property.PropertyName = "id";
            }

            return property;
        }
    }
}