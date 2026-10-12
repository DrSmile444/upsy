using Upsy.Core.Link;
using Upsy.Core.Protocol;
using Upsy.Infrastructure;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
using RadioState = Upsy.Core.Link.RadioState;

namespace Upsy.Ble;

/// <summary>Bluetooth LE transport over WinRT: scan by name, connect unpaired, Modbus over ff02/ff01.</summary>
public sealed class WinRtStationTransport : IStationTransport
{
    public const string NamePrefix = "EB3A";
    public static readonly Guid ServiceUuid = Guid.Parse("0000ff00-0000-1000-8000-00805f9b34fb");
    public static readonly Guid NotifyUuid = Guid.Parse("0000ff01-0000-1000-8000-00805f9b34fb");
    public static readonly Guid WriteUuid = Guid.Parse("0000ff02-0000-1000-8000-00805f9b34fb");
    private static readonly TimeSpan ScanWindow = TimeSpan.FromSeconds(10);

    public async Task<RadioState> GetRadioStateAsync(CancellationToken ct)
    {
        var radios = await Radio.GetRadiosAsync().AsTask(ct);
        var bt = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        return bt is null ? RadioState.Unavailable : bt.State == Windows.Devices.Radios.RadioState.On ? RadioState.On : RadioState.Off;
    }

    public async Task<IStationSession?> ConnectAsync(ulong? knownAddress, CancellationToken ct)
    {
        if (knownAddress is { } known && await TryConnectAsync(known, ct) is { } direct)
        {
            return direct;
        }

        var found = await ScanAsync(ct);
        return found is null ? null : await TryConnectAsync(found.Value, ct);
    }

    private static async Task<ulong?> ScanAsync(CancellationToken ct)
    {
        var result = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.Received += (_, e) =>
        {
            if (e.Advertisement.LocalName?.StartsWith(NamePrefix, StringComparison.Ordinal) == true)
            {
                result.TrySetResult(e.BluetoothAddress);
            }
        };
        watcher.Start();
        try
        {
            var winner = await Task.WhenAny(result.Task, Task.Delay(ScanWindow, ct));
            ct.ThrowIfCancellationRequested();
            return winner == result.Task ? result.Task.Result : null;
        }
        finally
        {
            watcher.Stop();
        }
    }

    private static async Task<IStationSession?> TryConnectAsync(ulong address, CancellationToken ct)
    {
        BluetoothLEDevice? device = null;
        try
        {
            device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(ct);
            if (device is null)
            {
                return null;
            }

            // Connects can fail transiently right after the station wakes; one quick retry covers it.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var services = await device.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached).AsTask(ct);
                if (services.Status == GattCommunicationStatus.Success && services.Services.Count > 0)
                {
                    var service = services.Services[0];
                    var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct);
                    var notify = chars.Characteristics.FirstOrDefault(c => c.Uuid == NotifyUuid);
                    var write = chars.Characteristics.FirstOrDefault(c => c.Uuid == WriteUuid);
                    if (notify is null || write is null)
                    {
                        service.Dispose();
                        return Fail(device);
                    }

                    var session = new WinRtStationSession(device, service, notify, write);
                    if (await session.SubscribeAsync(ct))
                    {
                        device = null; // owned by the session now
                        return session;
                    }

                    await session.DisposeAsync();
                    return null;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            return Fail(device);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn($"connect {address:X12} failed: {ex.Message}");
            device?.Dispose();
            return null;
        }
    }

    private static IStationSession? Fail(BluetoothLEDevice device)
    {
        device.Dispose();
        return null;
    }
}

internal sealed class WinRtStationSession : IStationSession
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);
    private readonly BluetoothLEDevice _device;
    private readonly GattDeviceService _service;
    private readonly GattCharacteristic _notify;
    private readonly GattCharacteristic _write;
    private readonly SemaphoreSlim _sequential = new(1, 1);
    private readonly FrameAssembler _assembler = new();
    private readonly object _gate = new();
    private TaskCompletionSource<byte[]>? _pending;

    public WinRtStationSession(BluetoothLEDevice device, GattDeviceService service, GattCharacteristic notify, GattCharacteristic write)
    {
        _device = device;
        _service = service;
        _notify = notify;
        _write = write;
        _notify.ValueChanged += OnValueChanged;
    }

    public ulong Address => _device.BluetoothAddress;

    public bool IsConnected => _device.ConnectionStatus == BluetoothConnectionStatus.Connected;

    public async Task<bool> SubscribeAsync(CancellationToken ct)
    {
        var status = await _notify.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(ct);
        return status == GattCommunicationStatus.Success;
    }

    public async Task<ushort[]?> ReadAsync(ushort start, ushort count, CancellationToken ct)
    {
        await _sequential.WaitAsync(ct);
        try
        {
            var pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _assembler.Reset();
                _pending = pending;
            }

            using var writer = new DataWriter();
            writer.WriteBytes(Modbus.BuildReadRequest(start, count));
            var status = await _write.WriteValueWithResultAsync(writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse).AsTask(ct);
            if (status.Status != GattCommunicationStatus.Success)
            {
                return null;
            }

            var done = await Task.WhenAny(pending.Task, Task.Delay(RequestTimeout, ct));
            ct.ThrowIfCancellationRequested();
            if (done != pending.Task)
            {
                Log.Warn($"read {start}+{count} timed out");
                return null;
            }

            var result = Modbus.ParseReadResponse(pending.Task.Result, count);
            if (!result.IsSuccess)
            {
                Log.Warn($"read {start}+{count}: {result.Error}");
            }

            return result.Registers;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn($"read {start}+{count} failed: {ex.Message}");
            return null;
        }
        finally
        {
            lock (_gate)
            {
                _pending = null;
            }

            _sequential.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _notify.ValueChanged -= OnValueChanged;
        _service.Dispose();
        _device.Dispose();
        _sequential.Dispose();
        return ValueTask.CompletedTask;
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var reader = DataReader.FromBuffer(args.CharacteristicValue);
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        lock (_gate)
        {
            if (_assembler.Append(bytes) is { } frame)
            {
                _pending?.TrySetResult(frame);
            }
        }
    }
}
