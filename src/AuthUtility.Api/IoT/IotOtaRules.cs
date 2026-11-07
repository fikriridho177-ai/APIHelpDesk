using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.IoT;

public static class IotOtaRules
{
    public static bool TryTransition(
        IotOtaStatus current,
        IotOtaEventType requested,
        int progress,
        out IotOtaStatus next,
        out string error)
    {
        next = current;
        error = "";

        if (progress is < 0 or > 100)
        {
            error = "Progress OTA harus berada di antara 0 dan 100.";
            return false;
        }

        if (requested == IotOtaEventType.Failed)
        {
            if (current is IotOtaStatus.Succeeded or IotOtaStatus.Failed)
            {
                error = "Job OTA terminal tidak dapat diubah lagi.";
                return false;
            }

            next = IotOtaStatus.Failed;
            return true;
        }

        next = (current, requested) switch
        {
            (IotOtaStatus.Queued, IotOtaEventType.Downloading) => IotOtaStatus.Downloading,
            (IotOtaStatus.Downloading, IotOtaEventType.Verifying) => IotOtaStatus.Verifying,
            (IotOtaStatus.Verifying, IotOtaEventType.Installing) => IotOtaStatus.Installing,
            (IotOtaStatus.Installing, IotOtaEventType.Rebooting) => IotOtaStatus.Rebooting,
            (IotOtaStatus.Rebooting, IotOtaEventType.WaitingConfirmation) => IotOtaStatus.WaitingConfirmation,
            (IotOtaStatus.WaitingConfirmation, IotOtaEventType.Confirmed) => IotOtaStatus.Succeeded,
            _ => current
        };

        if (next == current)
        {
            error = "Urutan OTA tidak valid.";
            return false;
        }

        if (requested == IotOtaEventType.Confirmed && progress != 100)
        {
            error = "Konfirmasi OTA harus melaporkan progress 100.";
            next = current;
            return false;
        }

        return true;
    }

    public static bool TryConfirm(
        string expectedVersion,
        string reportedVersion,
        out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(expectedVersion)
            || string.IsNullOrWhiteSpace(reportedVersion)
            || !string.Equals(expectedVersion.Trim(), reportedVersion.Trim(), StringComparison.Ordinal))
        {
            error = "Versi firmware setelah reboot tidak sesuai dengan target OTA.";
            return false;
        }

        return true;
    }

    public static bool IsSupportedTargetMcu(string? targetMcu) =>
        string.Equals(targetMcu?.Trim(), "ESP8266", StringComparison.OrdinalIgnoreCase);

    public static bool IsTimedOut(DateTimeOffset lastActivity, DateTimeOffset now, TimeSpan timeout) =>
        timeout > TimeSpan.Zero && now >= lastActivity && now - lastActivity > timeout;
}
