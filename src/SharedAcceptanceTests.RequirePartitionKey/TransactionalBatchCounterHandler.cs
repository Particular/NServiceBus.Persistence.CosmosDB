using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

class TransactionalBatchCounterHandler : RequestHandler
{
    public static double TotalTransactionalBatches;

    public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (request.Headers.TryGetValue("x-ms-cosmos-is-batch-request", out var _))
        {
            TotalTransactionalBatches++;
        }

        return response;
    }
}