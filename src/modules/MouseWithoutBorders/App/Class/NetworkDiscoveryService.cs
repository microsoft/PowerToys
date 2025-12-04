using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace MouseWithoutBorders.Class
{
    internal class DiscoveredDevice
    {
        public string DeviceName { get; set; }
        public string IpAddress { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsOnline => (DateTime.Now - LastSeen).TotalSeconds < 60;
        public string MachineId { get; set; }
        public string Version { get; set; }
    }

    internal class NetworkDiscoveryService : IDisposable
    {
        private const int BROADCAST_PORT = 24802;
        private const int DISCOVERY_INTERVAL_MS = 30000;
        private readonly UdpClient _broadcastClient;
        private readonly UdpClient _listenClient;
        private readonly Dictionary<string, DiscoveredDevice> _devices;
        private readonly Timer _broadcastTimer;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public event EventHandler<DiscoveredDevice> DeviceDiscovered;
        public event EventHandler<string> DeviceRemoved;

        public NetworkDiscoveryService()
        {
            _devices = new Dictionary<string, DiscoveredDevice>();
            _cts = new CancellationTokenSource();
            
            try
            {
                _broadcastClient = new UdpClient();
                _broadcastClient.EnableBroadcast = true;
                
                _listenClient = new UdpClient(BROADCAST_PORT);
                _listenClient.EnableBroadcast = true;
                
                _broadcastTimer = new Timer(BroadcastPresence, null, 0, DISCOVERY_INTERVAL_MS);
                
                Task.Run(() => ListenForDevices(_cts.Token));
                Task.Run(() => CleanupStaleDevices(_cts.Token));
            }
            catch (SocketException ex)
            {
                Common.Log($"Network discovery init failed: {ex.Message}");
            }
        }

        private void BroadcastPresence(object state)
        {
            try
            {
                var announcement = new
                {
                    Type = "MWB_DISCOVERY",
                    DeviceName = Environment.MachineName,
                    MachineId = GetMachineId(),
                    Version = "2.2",
                    Timestamp = DateTime.Now.Ticks
                };

                var json = JsonSerializer.Serialize(announcement);
                var data = Encoding.UTF8.GetBytes(json);
                
                var endpoint = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
                _broadcastClient?.Send(data, data.Length, endpoint);
            }
            catch (Exception ex)
            {
                Common.Log($"Broadcast error: {ex.Message}");
            }
        }

        private async Task ListenForDevices(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _listenClient.ReceiveAsync();
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    
                    var message = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (message != null && 
                        message.TryGetValue("Type", out var type) && 
                        type.ToString() == "MWB_DISCOVERY")
                    {
                        var deviceName = message["DeviceName"].ToString();
                        var machineId = message["MachineId"].ToString();
                        
                        if (machineId != GetMachineId())
                        {
                            var device = new DiscoveredDevice
                            {
                                DeviceName = deviceName,
                                IpAddress = result.RemoteEndPoint.Address.ToString(),
                                LastSeen = DateTime.Now,
                                MachineId = machineId,
                                Version = message.TryGetValue("Version", out var ver) ? ver.ToString() : "Unknown"
                            };

                            lock (_devices)
                            {
                                var isNew = !_devices.ContainsKey(machineId);
                                _devices[machineId] = device;
                                
                                if (isNew)
                                {
                                    DeviceDiscovered?.Invoke(this, device);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Common.Log($"Listen error: {ex.Message}");
                    }
                }
            }
        }

        private async Task CleanupStaleDevices(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000, token);
                    
                    lock (_devices)
                    {
                        var staleDevices = _devices
                            .Where(d => !d.Value.IsOnline)
                            .Select(d => d.Key)
                            .ToList();

                        foreach (var machineId in staleDevices)
                        {
                            _devices.Remove(machineId);
                            DeviceRemoved?.Invoke(this, machineId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Common.Log($"Cleanup error: {ex.Message}");
                    }
                }
            }
        }

        public IEnumerable<DiscoveredDevice> GetDiscoveredDevices()
        {
            lock (_devices)
            {
                return _devices.Values.Where(d => d.IsOnline).ToList();
            }
        }

        private string GetMachineId()
        {
            try
            {
                var mac = System.Net.NetworkInformation.NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    .Select(n => n.GetPhysicalAddress().ToString())
                    .FirstOrDefault();
                
                return mac ?? Environment.MachineName;
            }
            catch
            {
                return Environment.MachineName;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _cts?.Cancel();
            _broadcastTimer?.Dispose();
            _broadcastClient?.Close();
            _listenClient?.Close();
            _cts?.Dispose();
            
            _disposed = true;
        }
    }
}
