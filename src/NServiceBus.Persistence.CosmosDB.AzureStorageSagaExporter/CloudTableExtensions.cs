namespace NServiceBus.Persistence.CosmosDB.AzureStorageSagaExporter
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Table;

    static class CloudTableExtensions
    {
        public static async IAsyncEnumerable<T> ExecuteQueryAsync<T>(this CloudTable table, TableQuery<T> query, int take = int.MaxValue, [EnumeratorCancellation] CancellationToken ct = default) where T : ITableEntity, new()
        {
            TableContinuationToken token = null;
            var alreadyTaken = 0;

            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(
                        query: query,
                        token: token,
                        requestOptions: null,
                        operationContext: null,
                        cancellationToken: ct)
                    .ConfigureAwait(false);
                token = seg.ContinuationToken;

                foreach (var entity in seg.Results)
                {
                    if (alreadyTaken < take && !ct.IsCancellationRequested)
                    {
                        alreadyTaken++;
                        yield return entity;
                    }
                    else
                    {
                        break;
                    }
                }
            } while (token != null && !ct.IsCancellationRequested && alreadyTaken < take);
        }
    }
}