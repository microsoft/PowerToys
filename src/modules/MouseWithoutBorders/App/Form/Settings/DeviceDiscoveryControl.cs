using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.Form.Settings
{
    internal partial class DeviceDiscoveryControl : UserControl
    {
        private NetworkDiscoveryService _discoveryService;
        private ListView _deviceList;
        private Label _statusLabel;
        private Button _refreshButton;
        private Timer _updateTimer;

        public event EventHandler<DiscoveredDevice> DeviceSelected;

        public DeviceDiscoveryControl()
        {
            InitializeComponent();
            SetupUI();
            StartDiscovery();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            Name = "DeviceDiscoveryControl";
            Size = new Size(500, 400);
            
            ResumeLayout(false);
        }

        private void SetupUI()
        {
            _statusLabel = new Label
            {
                Text = "Searching for devices on network...",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            _deviceList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            _deviceList.Columns.Add("Device Name", 150);
            _deviceList.Columns.Add("IP Address", 120);
            _deviceList.Columns.Add("Status", 80);
            _deviceList.Columns.Add("Version", 80);
            _deviceList.DoubleClick += DeviceList_DoubleClick;

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            _refreshButton = new Button
            {
                Text = "Refresh",
                Width = 100,
                Height = 30,
                Location = new Point(10, 10)
            };
            _refreshButton.Click += RefreshButton_Click;

            buttonPanel.Controls.Add(_refreshButton);

            Controls.Add(_deviceList);
            Controls.Add(_statusLabel);
            Controls.Add(buttonPanel);

            _updateTimer = new Timer
            {
                Interval = 2000,
                Enabled = true
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        private void StartDiscovery()
        {
            try
            {
                _discoveryService = new NetworkDiscoveryService();
                _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
                _discoveryService.DeviceRemoved += OnDeviceRemoved;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Discovery failed: {ex.Message}";
                Common.Log($"Discovery service error: {ex}");
            }
        }

        private void OnDeviceDiscovered(object sender, DiscoveredDevice device)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnDeviceDiscovered(sender, device)));
                return;
            }

            UpdateDeviceList();
        }

        private void OnDeviceRemoved(object sender, string machineId)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnDeviceRemoved(sender, machineId)));
                return;
            }

            UpdateDeviceList();
        }

        private void UpdateDeviceList()
        {
            if (_discoveryService == null) return;

            _deviceList.BeginUpdate();
            _deviceList.Items.Clear();

            var devices = _discoveryService.GetDiscoveredDevices().ToList();
            
            foreach (var device in devices)
            {
                var item = new ListViewItem(device.DeviceName);
                item.SubItems.Add(device.IpAddress);
                item.SubItems.Add("Online");
                item.SubItems.Add(device.Version);
                item.Tag = device;
                item.ForeColor = Color.DarkGreen;
                _deviceList.Items.Add(item);
            }

            _statusLabel.Text = devices.Any() 
                ? $"Found {devices.Count} device(s) - Double-click to connect"
                : "No devices found. Make sure other devices are running Mouse Without Borders.";

            _deviceList.EndUpdate();
        }

        private void DeviceList_DoubleClick(object sender, EventArgs e)
        {
            if (_deviceList.SelectedItems.Count > 0)
            {
                var device = _deviceList.SelectedItems[0].Tag as DiscoveredDevice;
                if (device != null)
                {
                    DeviceSelected?.Invoke(this, device);
                }
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            UpdateDeviceList();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateDeviceList();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _discoveryService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
