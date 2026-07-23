using DevHunt.CoreApi.Services;
using FluentAssertions;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

public class EventBusStartupValidationTests
{
    [Fact]
    public void EnsureProductionConfiguration_throws_when_rabbitmq_missing_in_production()
    {
        var act = () => EventBusStartupValidation.EnsureProductionConfiguration(
            isProduction: true,
            eventBusFeatureEnabled: true,
            rabbitMqConnectionString: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RabbitMQ:ConnectionString*");
    }

    [Fact]
    public void EnsureProductionConfiguration_allows_noop_when_feature_disabled()
    {
        var act = () => EventBusStartupValidation.EnsureProductionConfiguration(
            isProduction: true,
            eventBusFeatureEnabled: false,
            rabbitMqConnectionString: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureProductionConfiguration_allows_missing_rabbitmq_in_development()
    {
        var act = () => EventBusStartupValidation.EnsureProductionConfiguration(
            isProduction: false,
            eventBusFeatureEnabled: true,
            rabbitMqConnectionString: null);

        act.Should().NotThrow();
    }
}
