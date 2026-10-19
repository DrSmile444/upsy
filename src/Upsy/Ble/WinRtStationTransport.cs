using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Channels;
using Upsy.Core.Link;
using Upsy.Core.Station;
using Upsy.Infrastructure;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
using RadioState = Upsy.Core.Link.RadioState;

namespace Upsy.Ble;

/// <summary>Bluetooth LE over WinRT: connects unpaired by address and opens the model's GATT service as a byte channel.</summary>
public sealed class WinRtStationTransport : IStationTransport
{
    public async Task<RadioState> GetRadioStateAsync(CancellationToken ct)
    {
        var radios = await Radio.GetRadiosAsync().AsTask(ct);
        var bt = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        return bt is null ? RadioState.Unavailable : bt.State == Windows.Devices.Radios.RadioState.On ? RadioState.On : RadioState.Off;
    }

    public async Task<IGattChannel?> OpenAsync(ulong address, GattProfile profile, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profile);
        BluetoothLEDevice? device = null;
        try
        {
            device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(ct);
            if (device is null)
            {
                return null;
            }

            // Connects can fail transiently right after the station wakes; one quick retry covers it. A DELTA 2 that just
            // dropped a link returns no services for a while; the monitor's backoff handles that.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var services = await device.GetGattServicesForUuidAsync(profile.Service, BluetoothCacheMode.Uncached).AsTask(ct);
                if (services.Status == GattCommunicationStatus.Success && services.Services.Count > 0)
                {
                    var service = services.Services[0];
                    var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct);
                    var notify = chars.Characteristics.FirstOrDefault(c => c.Uuid == profile.Notify);
                    var write = chars.Characteristics.FirstOrDefault(c => c.Uuid == profile.Write);
                    if (notify is null || write is null)
                    {
                        service.Dispose();
                        device.Dispose();
                        return null;
                    }

                    var channel = new WinRtGattChannel(device, service, notify, write);
                    if (await channel.SubscribeAsync(ct))
                    {
                        device = null; // owned by the channel now
                        return channel;
                    }

                    await channel.DisposeAsync();
                    return null;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            device.Dispose();
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn($"connect {address:X12} failed: {ex.Message}");
            device?.Dispose();
            return null;
        }
    }
}

internal sealed class WinRtGattChannel : IGattChannel
{
    private readonly BluetoothLEDevice _device;
    private readonly GattDeviceService _service;
    private readonly GattCharacteristic _notify;
    private readonly GattCharacteristic _write;
    private readonly Channel<byte[]> _notifications = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    public WinRtGattChannel(BluetoothLEDevice device, GattDeviceService service, GattCharacteristic notify, GattCharacteristic write)
    {
        _device = device;
        _service = service;
        _notify = notify;
        _write = write;
        _notify.ValueChanged += OnValueChanged;
        _device.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    public ulong Address => _device.BluetoothAddress;

    public bool IsConnected => _device.ConnectionStatus == BluetoothConnectionStatus.Connected;

    public ChannelReader<byte[]> Notifications => _notifications.Reader;

    public async Task<bool> SubscribeAsync(CancellationToken ct)
    {
        var status = await _notify.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(ct);
        return status == GattCommunicationStatus.Success;
    }

    public async Task<bool> WriteAsync(ReadOnlyMemory<byte> data, bool withResponse, CancellationToken ct)
    {
        try
        {
            var result = await _write.WriteValueWithResultAsync(
                data.ToArray().AsBuffer(),
                withResponse ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse).AsTask(ct);
            if (result.Status != GattCommunicationStatus.Success)
            {
                Log.Warn($"write to {Address:X12}: {result.Status}");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn($"write to {Address:X12} failed: {ex.Message}");
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        _notify.ValueChanged -= OnValueChanged;
        _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _notifications.Writer.TryComplete();
        _service.Dispose();
        _device.Dispose();
        return ValueTask.CompletedTask;
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args) =>
        _notifications.Writer.TryWrite(args.CharacteristicValue.ToArray());

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            // Wake a driver waiting for notifications; it ends and the monitor reconnects.
            _notifications.Writer.TryComplete();
        }
    }
}

/// <summary>The app's one BLE scanner (active scanning); every advertisement is raised as <see cref="Received"/>.</summary>
public sealed class AdvertisementHub : IAdvertisementSource, IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher = new() { ScanningMode = BluetoothLEScanningMode.Active };

    public AdvertisementHub()
    {
        _watcher.Received += OnReceived;
        _watcher.Stopped += (_, e) =>
        {
            if (e.Error != BluetoothError.Success)
            {
                Log.Warn($"scanner stopped: {e.Error}");
            }
        };
    }

    public event EventHandler<Advertisement>? Received;

    /// <summary>Starts scanning; harmless when it already runs. Call again after the radio comes back.</summary>
    public void Start()
    {
        if (_watcher.Status != BluetoothLEAdvertisementWatcherStatus.Started)
        {
            try
            {
                _watcher.Start();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
                Log.Warn($"scanner did not start: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _watcher.Received -= OnReceived;
        _watcher.Stop();
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs e)
    {
        var data = new Dictionary<ushort, byte[]>();
        foreach (var m in e.Advertisement.ManufacturerData)
        {
            data[m.CompanyId] = m.Data.ToArray();
        }

        Received?.Invoke(this, new Advertisement(e.BluetoothAddress, string.IsNullOrEmpty(e.Advertisement.LocalName) ? null : e.Advertisement.LocalName, data, e.RawSignalStrengthInDBm));
    }
}
