namespace NServiceBus.Persistence.ComponentTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class ParallelPerformanceTest : SagaPersisterTests<TestSaga, TestSagaData>
    {
        /*
            This test performs 4 operations per saga instance
                - Get
                - Save
                - Get
                - Complete

            The saga lifecycles are executed in parallel
           
            This test proofs that performance is only limited by the number of RU's provisioned.    

            Test run durations should be similar when you scale the values linearly with the provisioned RU's
            e.g.
                400 RU's provisioned (the default), should allow to execute 1000 saga's, 50 in parallel in the same time as. 
                4000 RU's would execute 10000 saga's, 500 in parallel.

            When you do to many in parallel you will get statuscode 429, subcode 3200. 

            Which means you hit the maximum number of parallel requests allowed by the provisioned RU's.            
       
            Results (measured from belgium to EU west)
            - 400 RU's, 1000 saga's, 50 in parallel: 50 seconds execution: 80 saga's per second or 320 operations per second. (close to what RU's allow)
            - 4000 RU's, 10000 saga's, 500 in parallel: 50 seconds execution: 800 saga's per second or 3200 operations per second. (close to what RU's allow)
         */

        [Test]
        public async Task Run()
        {
            var numberOfSagas = 1000;
            var sagasInParallel = 50; // this depends on RU's provisioned, 50 seems to be ideal for the default 400 RU's provisioned, 500 for 4000 RU's
            var batchCounter = 0;
            var waitFor = new List<Task>();

            for (var i = 0; i < numberOfSagas; i++)
            {
                waitFor.Add(Task.Run(async() =>
                {
                    var correlationPropertyData = Guid.NewGuid().ToString();

                    var saga = new TestSagaData { SomeId = correlationPropertyData, DateTimeProperty = DateTime.UtcNow };

                    await SaveSaga(saga);

                    await GetByIdAndComplete(saga.Id);
                }));

                if(batchCounter == sagasInParallel)
                {
                    await Task.WhenAll(waitFor);
                    waitFor.Clear();
                    batchCounter = 0;
                }
                else
                {
                    batchCounter++;
                }
            }

            await Task.WhenAll(waitFor);
        }
    }
}