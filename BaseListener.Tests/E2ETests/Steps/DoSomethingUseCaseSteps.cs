using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using AutoFixture;
using BaseListener.Boundary;
using BaseListener.Domain;
using BaseListener.Infrastructure;
using BaseListener.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BaseListener.Tests.E2ETests.Steps
{
    public class DoSomethingUseCaseSteps : BaseSteps
    {
        private readonly Fixture _fixture = new Fixture();
        private Exception _lastException;

        public DoSomethingUseCaseSteps()
        { }

        private SQSEvent.SQSMessage CreateMessage(Guid personId, string eventType = EventTypes.DoSomethingEvent)
        {
            var personSns = _fixture.Build<EntityEventSns>()
                                    .With(x => x.EntityId, personId)
                                    .With(x => x.EventType, eventType)
                                    .Create();

            var msgBody = JsonSerializer.Serialize(personSns, _jsonOptions);
            return _fixture.Build<SQSEvent.SQSMessage>()
                           .With(x => x.Body, msgBody)
                           .With(x => x.MessageAttributes, new Dictionary<string, SQSEvent.MessageAttribute>())
                           .Create();
        }

        public async Task WhenTheFunctionIsTriggered(Guid id)
        {
            var mockLambdaLogger = new Mock<ILambdaLogger>();
            ILambdaContext lambdaContext = new TestLambdaContext()
            {
                Logger = mockLambdaLogger.Object
            };

            var sqsEvent = _fixture.Build<SQSEvent>()
                                   .With(x => x.Records, new List<SQSEvent.SQSMessage> { CreateMessage(id) })
                                   .Create();

            Func<Task> func = async () =>
            {
                var fn = new SqsFunction();
                await fn.FunctionHandler(sqsEvent, lambdaContext).ConfigureAwait(false);
            };

            _lastException = await Record.ExceptionAsync(func);
        }

        public async Task ThenTheEntityIsUpdated(DbEntity beforeChange, IDynamoDBContext dbContext)
        {
            var entityInDb = await dbContext.LoadAsync<DbEntity>(beforeChange.Id);

            entityInDb.Should().BeEquivalentTo(beforeChange,
                config => config.Excluding(y => y.Description)
                                .Excluding(z => z.VersionNumber));
            entityInDb.Description.Should().Be("Updated");
            entityInDb.VersionNumber.Should().Be(beforeChange.VersionNumber + 1);
        }

        public void ThenAnEntityNotFoundExceptionIsThrown(Guid id)
        {
            _lastException.Should().NotBeNull();
            _lastException.Should().BeOfType(typeof(EntityNotFoundException<DomainEntity>));
            (_lastException as EntityNotFoundException<DomainEntity>).Id.Should().Be(id);
        }
    }
}
