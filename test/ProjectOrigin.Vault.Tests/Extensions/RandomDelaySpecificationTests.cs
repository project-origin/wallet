using System.Threading.Tasks;
using MassTransit;
using ProjectOrigin.Vault.Extensions;
using Xunit;

namespace ProjectOrigin.Vault.Tests.Extensions
{
    public class RandomDelaySpecificationTests
    {
        [Fact]
        public void Validate_ShouldReturnErrorIfMinDelayIsNegative()
        {
            var specification = new RandomDelaySpecification<TestConsumer, TestMessage>(-1, 100);

            var error = Assert.Single(specification.Validate());

            Assert.Equal("RandomDelayFilter", error.Key);
            Assert.Equal("minDelayInMilliseconds cannot be negative", error.Message);
        }

        [Fact]
        public void Validate_ShouldReturnErrorIfMaxDelayIsLessThanOrEqualToMinDelay()
        {
            var specification = new RandomDelaySpecification<TestConsumer, TestMessage>(5, 5);

            var error = Assert.Single(specification.Validate());

            Assert.Equal("RandomDelayFilter", error.Key);
            Assert.Equal("maxDelayInMilliseconds must be greater than minDelayInMilliseconds", error.Message);
        }

        [Fact]
        public void Validate_ShouldReturnNoErrorsForValidRange()
        {
            var specification = new RandomDelaySpecification<TestConsumer, TestMessage>(0, 10);

            Assert.Empty(specification.Validate());
        }

        private class TestMessage;

        private class TestConsumer : IConsumer<TestMessage>
        {
            public Task Consume(ConsumeContext<TestMessage> context) => Task.CompletedTask;
        }
    }
}
