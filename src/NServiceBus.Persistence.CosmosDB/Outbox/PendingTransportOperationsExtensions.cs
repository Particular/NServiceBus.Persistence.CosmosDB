namespace NServiceBus.Persistence.CosmosDB;

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Transport;

static class PendingTransportOperationsExtensions
{
    static PendingTransportOperationsExtensions()
    {
        FieldInfo field = typeof(PendingTransportOperations).GetField("operations",
            BindingFlags.NonPublic | BindingFlags.Instance);
        ParameterExpression targetExp = Expression.Parameter(typeof(PendingTransportOperations), "target");
        MemberExpression fieldExp = Expression.Field(targetExp, field);
        getter = Expression
            .Lambda<Func<PendingTransportOperations, ConcurrentStack<TransportOperation>>>(fieldExp, targetExp)
            .Compile();
    }

    public static void Clear(this PendingTransportOperations operations)
    {
        ConcurrentStack<TransportOperation> collection = getter(operations);
        collection.Clear();
    }

    static readonly Func<PendingTransportOperations, ConcurrentStack<TransportOperation>> getter;
}