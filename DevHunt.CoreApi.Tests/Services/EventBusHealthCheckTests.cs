using DevHunt.CoreApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace DevHunt.CoreApi.Tests.Services;

public class EventBusHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_event_bus_active()
    {
        var check = new EventBusHealthCheck(new EventBusRuntimeState(isActive: true));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_degraded_when_event_bus_noop()
    {
        var check = new EventBusHealthCheck(new EventBusRuntimeState(isActive: false));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data.Should().ContainKey("event_bus_mode").WhoseValue.Should().Be("noop");
    }
}
