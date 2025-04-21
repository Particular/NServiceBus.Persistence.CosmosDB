﻿namespace NServiceBus.Persistence.CosmosDB;

using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

class UpperCaseIdIntoLowerCaseIdContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);

        if (member.Name == "Id")
        {
            property.PropertyName = "id";
        }

        return property;
    }
}