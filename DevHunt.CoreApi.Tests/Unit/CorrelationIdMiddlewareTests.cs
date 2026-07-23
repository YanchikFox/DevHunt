using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using DevHunt.CoreApi.Middleware;
using System.Threading.Tasks;

namespace DevHunt.CoreApi.Tests.Unit;

/// <summary>
/// Tests for CorrelationIdMiddleware.
/// Cover generation and propagation of correlation IDs across requests.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNoCorrelationId_ShouldGenerateNewIdAsync()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var requestDelegate = new Mock<RequestDelegate>();
        
        var middleware = new CorrelationIdMiddleware(requestDelegate.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-Correlation-ID");
        context.Items.Should().ContainKey("CorrelationId");
        context.Items["CorrelationId"].Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdProvided_ShouldUseProvidedIdAsync()
    {
        // Arrange
        var providedId = "test-correlation-id-123";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = providedId;
        
        var requestDelegate = new Mock<RequestDelegate>();
        var middleware = new CorrelationIdMiddleware(requestDelegate.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Correlation-ID"].ToString().Should().Be(providedId);
        context.Items["CorrelationId"].Should().Be(providedId);
    }

    [Fact]
    public async Task InvokeAsync_WhenXRequestIdProvided_ShouldUseRequestIdAsync()
    {
        // Arrange
        var providedId = "test-request-id-456";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = providedId;
        
        var requestDelegate = new Mock<RequestDelegate>();
        var middleware = new CorrelationIdMiddleware(requestDelegate.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Correlation-ID"].ToString().Should().Be(providedId);
        context.Response.Headers["X-Request-ID"].ToString().Should().Be(providedId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddlewareAsync()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var requestDelegateCalled = false;
        
        RequestDelegate requestDelegate = async (ctx) =>
        {
            requestDelegateCalled = true;
            await Task.CompletedTask;
        };
        
        var middleware = new CorrelationIdMiddleware(requestDelegate);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        requestDelegateCalled.Should().BeTrue();
    }
}

