namespace NServiceBus.Persistence.ComponentTests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class SerialPerformanceTest : SagaPersisterTests<TestSaga, TestSagaData>
    {
        /*
            
            This test performs 4 operations per saga instance
                - Get
                - Save
                - Get
                - Complete

            The saga lifecycles are executed serially.

            This test is limited by latency, no matter how many RU's are provisioned, performance stays the same. (far less then the RU's allow).       
       
            Results (measured from belgium to EU west)
            - 400 RU's, 100 saga's. 11 seconds execution: 9 saga's per second or 36 operations per second.
            - 4000 RU's, 10000 saga's. 17 minutes execution: 9 saga's per second or 36 operations per second.
         */

        [Test]
        public async Task Run()
        {
            var iterations = 100;

            for (var i = 0; i < iterations; i++)
            {
                var correlationPropertyData = Guid.NewGuid().ToString();

                var saga = new TestSagaData { SomeId = correlationPropertyData, DateTimeProperty = DateTime.UtcNow };

                await SaveSaga(saga);

                await GetByIdAndComplete(saga.Id);
            }
        }
    }
}