using LimelightX.UI.Services;
using Xunit;

namespace LimelightX.UI.Tests.Services;

public class ExecutionLockServiceTests
{
    [Fact]
    public void TryAcquire_NoHolder_Succeeds()
    {
        var lockService = new ExecutionLockService();
        var token = new object();

        Assert.True(lockService.TryAcquire(token));
        Assert.True(lockService.IsAnyExecutionRunning);
    }

    [Fact]
    public void TryAcquire_AlreadyHeldByOtherToken_Fails()
    {
        var lockService = new ExecutionLockService();
        lockService.TryAcquire(new object());

        Assert.False(lockService.TryAcquire(new object()));
    }

    [Fact]
    public void Release_ByHolder_ClearsLock()
    {
        var lockService = new ExecutionLockService();
        var token = new object();
        lockService.TryAcquire(token);

        lockService.Release(token);

        Assert.False(lockService.IsAnyExecutionRunning);
    }

    [Fact]
    public void Release_ByNonHolder_IsNoOp()
    {
        var lockService = new ExecutionLockService();
        var holder = new object();
        lockService.TryAcquire(holder);

        lockService.Release(new object());

        Assert.True(lockService.IsAnyExecutionRunning);
    }

    [Fact]
    public void ExecutionLockChanged_FiresOnAcquireAndRelease()
    {
        var lockService = new ExecutionLockService();
        var token = new object();
        var raiseCount = 0;
        lockService.ExecutionLockChanged += () => raiseCount++;

        lockService.TryAcquire(token);
        lockService.Release(token);

        Assert.Equal(2, raiseCount);
    }

    [Fact]
    public void ExecutionLockChanged_DoesNotFireOnFailedAcquireOrNoOpRelease()
    {
        var lockService = new ExecutionLockService();
        lockService.TryAcquire(new object());
        var raiseCount = 0;
        lockService.ExecutionLockChanged += () => raiseCount++;

        lockService.TryAcquire(new object());
        lockService.Release(new object());

        Assert.Equal(0, raiseCount);
    }
}
