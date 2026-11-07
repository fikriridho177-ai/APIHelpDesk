using AuthUtility.Api.IoT;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Tests;

public class IotOtaRulesTests
{
    [Fact]
    public void AllowsOnlyTheOrderedOtaLifecycle()
    {
        var status = IotOtaStatus.Queued;

        Assert.True(IotOtaRules.TryTransition(status, IotOtaEventType.Downloading, 0, out status, out var error), error);
        Assert.Equal(IotOtaStatus.Downloading, status);
        Assert.True(IotOtaRules.TryTransition(status, IotOtaEventType.Verifying, 45, out status, out error), error);
        Assert.Equal(IotOtaStatus.Verifying, status);
        Assert.True(IotOtaRules.TryTransition(status, IotOtaEventType.Installing, 75, out status, out error), error);
        Assert.Equal(IotOtaStatus.Installing, status);
        Assert.True(IotOtaRules.TryTransition(status, IotOtaEventType.Rebooting, 100, out status, out error), error);
        Assert.Equal(IotOtaStatus.Rebooting, status);
        Assert.True(IotOtaRules.TryTransition(status, IotOtaEventType.WaitingConfirmation, 100, out status, out error), error);
        Assert.Equal(IotOtaStatus.WaitingConfirmation, status);
        Assert.True(IotOtaRules.TryTransition(status, IotOtaEventType.Confirmed, 100, out status, out error), error);
        Assert.Equal(IotOtaStatus.Succeeded, status);
    }

    [Fact]
    public void RejectsSkippedBackwardAndInvalidProgressTransitions()
    {
        Assert.False(IotOtaRules.TryTransition(
            IotOtaStatus.Queued,
            IotOtaEventType.Installing,
            0,
            out var next,
            out var error));
        Assert.Equal(IotOtaStatus.Queued, next);
        Assert.Contains("urutan", error, StringComparison.OrdinalIgnoreCase);

        Assert.False(IotOtaRules.TryTransition(
            IotOtaStatus.Verifying,
            IotOtaEventType.Downloading,
            50,
            out next,
            out error));
        Assert.Contains("urutan", error, StringComparison.OrdinalIgnoreCase);

        Assert.False(IotOtaRules.TryTransition(
            IotOtaStatus.Downloading,
            IotOtaEventType.Verifying,
            101,
            out next,
            out error));
        Assert.Contains("progress", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequiresExactReportedVersionForConfirmation()
    {
        Assert.False(IotOtaRules.TryConfirm("1.2.0", "1.1.9", out var error));
        Assert.Contains("versi", error, StringComparison.OrdinalIgnoreCase);

        Assert.True(IotOtaRules.TryConfirm("1.2.0", "1.2.0", out error), error);
    }

    [Fact]
    public void SupportsOnlyEsp8266AsTheFirstOtaTarget()
    {
        Assert.True(IotOtaRules.IsSupportedTargetMcu("ESP8266"));
        Assert.False(IotOtaRules.IsSupportedTargetMcu("ATMEGA2560"));
        Assert.False(IotOtaRules.IsSupportedTargetMcu(""));
    }

    [Fact]
    public void DetectsStalledJobsUsingTheSuppliedTimeout()
    {
        var now = DateTimeOffset.Parse("2026-11-07T10:00:00Z");
        var lastActivity = now.AddMinutes(-11);

        Assert.True(IotOtaRules.IsTimedOut(lastActivity, now, TimeSpan.FromMinutes(10)));
        Assert.False(IotOtaRules.IsTimedOut(now.AddMinutes(-9), now, TimeSpan.FromMinutes(10)));
    }
}
