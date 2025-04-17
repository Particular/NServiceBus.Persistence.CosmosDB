using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

class TransactionalBatchCounterHandler : RequestHandler
{
    public static int TotalTransactionalBatches;

    public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken = default)
    {
        ResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (request.Headers.TryGetValue("x-ms-cosmos-is-batch-request", out _))
        {
            TotalTransactionalBatches++;
        }

        return response;
    }
}