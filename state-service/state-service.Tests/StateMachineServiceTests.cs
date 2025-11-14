using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StateService.Domain.Value;
using StateService.Infrastructure.Persistence;
using StateService.Infrastructure.Services;
using Xunit;

namespace StateService.Tests;

public class StateMachineServiceTests
{
    private static StateDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<StateDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new StateDbContext(options);
    }

    private static StateMachineService CreateService(StateDbContext context) =>
        new(context, NullLogger<StateMachineService>.Instance);

    [Fact]
    public async Task CreateAsync_ShouldPersistCorrelationId()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var service = CreateService(context);

        var pid = await service.CreateAsync("corr-123");

        var savedTask = await context.Tasks.FindAsync(pid);
        Assert.NotNull(savedTask);
        Assert.Equal("corr-123", savedTask!.CorrelationId);
    }

    [Fact]
    public async Task AdvanceAsync_ShouldStoreCorrelationIdWhenMissing()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var service = CreateService(context);

        var pid = await service.CreateAsync(null);
        var advanced = await service.AdvanceAsync(pid, TaskState.RouteFinding, "corr-456");

        Assert.True(advanced);

        var savedTask = await context.Tasks.FindAsync(pid);
        Assert.NotNull(savedTask);
        Assert.Equal("corr-456", savedTask!.CorrelationId);
        Assert.Equal(TaskState.RouteFinding, savedTask.CurrentState);
    }

    [Fact]
    public async Task GetPidByCorrelationIdAsync_ShouldReturnPidWhenExists()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var service = CreateService(context);

        var pid = await service.CreateAsync("corr-789");

        var result = await service.GetPidByCorrelationIdAsync("corr-789");
        var missing = await service.GetPidByCorrelationIdAsync("does-not-exist");

        Assert.Equal(pid, result);
        Assert.Null(missing);
    }
}

