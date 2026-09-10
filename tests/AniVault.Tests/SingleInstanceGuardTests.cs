using System;
using System.Threading;
using AniVault.Services;

namespace AniVault.Tests;

public class SingleInstanceGuardTests
{
    private static string UniqueName() => "AniVaultTest-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void First_Guard_Is_Primary_And_A_Second_Is_Not()
    {
        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);
        using var second = new SingleInstanceGuard(name);

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void Signalling_The_Primary_Raises_Its_Activation_Event()
    {
        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);

        using var raised = new ManualResetEventSlim(false);
        first.ActivationRequested += () => raised.Set();

        using (var second = new SingleInstanceGuard(name))
        {
            second.SignalPrimaryInstance();
        }

        Assert.True(raised.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void After_The_Primary_Is_Disposed_A_New_Guard_Becomes_Primary()
    {
        var name = UniqueName();

        var first = new SingleInstanceGuard(name);
        Assert.True(first.IsPrimary);
        first.Dispose();

        using var next = new SingleInstanceGuard(name);
        Assert.True(next.IsPrimary);
    }
}
