namespace XTouchVMBridge.Core.Interfaces;

public interface IAudioDeviceMonitor : IDisposable
{
    event EventHandler? DevicesChanged;

    int DeviceCount { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
