using IndustrialMonitor.Models;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;

namespace IndustrialMonitor
{
    public partial class MainWindow : Window
    {
        private SerialPort? _serialPort;
        public ObservableCollection<DeviceData> Devices { get; set; } = new ObservableCollection<DeviceData>();

        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            RefreshPortList();
            InitDevices();
        }

        private void InitDevices()
        {
            Devices.Clear();
            for (int i = 1; i <= 3; i++)
            {
                var device = new DeviceData
                {
                    DeviceId = i,
                    DeviceName = $"设备 {i}",
                    Registers = new ObservableCollection<RegisterData>()
                };
                for (int j = 0; j < 3; j++)
                {
                    device.Registers.Add(new RegisterData { Address = j, Value = 0 });
                }
                Devices.Add(device);
            }
        }

        /// <summary>
        /// 刷新串口列表
        /// </summary>
        private void RefreshPortList()
        {
            cmbPorts.Items.Clear();
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                cmbPorts.Items.Add(port);
            }
            if (cmbPorts.Items.Count > 0)
                cmbPorts.SelectedIndex = 0;

            AppendLog($"已刷新，找到 {cmbPorts.Items.Count} 个串口");
        }

        /// <summary>
        /// 打开/关闭串口
        /// </summary>
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                // 已打开 → 关闭
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
                btnConnect.Content = "打开串口";
                btnConnect.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(39, 174, 96));
                lblStatus.Content = "⚪ 未连接";
                lblStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(231, 76, 60));
                AppendLog("串口已关闭");
                return;
            }

            // 未打开 → 打开
            if (cmbPorts.SelectedItem == null)
            {
                MessageBox.Show("请先选择一个串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string portName = cmbPorts.SelectedItem.ToString()!;
                _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                btnConnect.Content = "关闭串口";
                btnConnect.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(231, 76, 60));
                lblStatus.Content = $"🟢 已连接 {portName}";
                lblStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(46, 204, 113));
                AppendLog($"成功打开串口 {portName}");

                // ===== 修改点：连接成功后，读取所有设备 =====
                BtnRead_Click(null!, null!);
                // ============================================
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"❌ 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 数据接收事件（异步）
        /// </summary>
        /// <summary>
        /// 数据接收事件（异步）
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var sp = (SerialPort)sender;
                int bytesToRead = sp.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                int read = sp.Read(buffer, 0, bytesToRead);

                Dispatcher.Invoke(() =>
                {
                    string hex = BitConverter.ToString(buffer).Replace("-", " ");
                    AppendLog($"📩 收到 {read} 字节: {hex}");

                    // 检查是否为 Modbus 响应（功能码 0x03）
                    if (read >= 5 && buffer[1] == 0x03)
                    {
                        int deviceId = buffer[0];          // 设备地址（1、2、3）
                        int dataLength = buffer[2];        // 数据字节数
                        int registerCount = dataLength / 2; // 寄存器个数

                        // 查找对应的设备
                        var device = Devices.FirstOrDefault(d => d.DeviceId == deviceId);
                        if (device != null)
                        {
                            // 先保存旧值用于变化检测
                            var oldValues = device.Registers.ToDictionary(r => r.Address, r => r.Value);

                            // 更新寄存器的值
                            for (int i = 0; i < registerCount; i++)
                            {
                                int value = (buffer[3 + i * 2] << 8) | buffer[4 + i * 2];
                                int address = i;

                                // 如果值发生变化，标记 HasChanged
                                bool hasChanged = false;
                                if (oldValues.TryGetValue(address, out int oldVal) && oldVal != value)
                                {
                                    hasChanged = true;
                                    AppendLog($"🔔 设备{deviceId} 寄存器{address} 变化: {oldVal} → {value}");
                                }

                                // 更新数据（如果该寄存器已存在）
                                if (address < device.Registers.Count)
                                {
                                    device.Registers[address].Value = value;
                                    device.Registers[address].HasChanged = hasChanged;
                                }
                                else
                                {
                                    // 如果寄存器数量超出，动态添加（兼容性处理）
                                    device.Registers.Add(new RegisterData
                                    {
                                        Address = address,
                                        Value = value,
                                        HasChanged = hasChanged
                                    });
                                }
                            }

                            AppendLog($"📥 设备 {deviceId} 数据已更新（{registerCount} 个寄存器）");

                            // 2秒后清除高亮
                            if (device.Registers.Any(r => r.HasChanged))
                            {
                                var timer = new System.Timers.Timer(2000);
                                timer.Elapsed += (s, ev) =>
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        foreach (var reg in device.Registers)
                                        {
                                            reg.HasChanged = false;
                                        }
                                    });
                                    timer.Stop();
                                    timer.Dispose();
                                };
                                timer.AutoReset = false;
                                timer.Start();
                            }
                        }
                        else
                        {
                            AppendLog($"⚠️ 收到未知设备 {deviceId} 的数据");
                        }
                    }

                    UpdateRecvCount(read);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendLog($"⚠️ 接收异常: {ex.Message}"));
            }
        }


        private async void BtnRead_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                AppendLog("⚠️ 请先打开串口");
                return;
            }

            // 轮询设备地址 1, 2, 3
            for (int deviceId = 1; deviceId <= 3; deviceId++)
            {
                byte[] command = new byte[] { (byte)deviceId, 0x03, 0x00, 0x00, 0x00, 0x03 };
                byte[] crc = Helpers.ModbusCRC.Calculate(command);
                byte[] fullCommand = new byte[command.Length + crc.Length];
                Array.Copy(command, 0, fullCommand, 0, command.Length);
                Array.Copy(crc, 0, fullCommand, command.Length, crc.Length);

                _serialPort.Write(fullCommand, 0, fullCommand.Length);
                AppendLog($"📤 发送指令 (设备{deviceId}): {BitConverter.ToString(fullCommand).Replace("-", " ")}");
                await Task.Delay(200);

            }
        }



        /// <summary>
        /// 刷新按钮
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortList();
        }

        /// <summary>
        /// 追加日志
        /// </summary>
        private void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}\n");
            txtLog.ScrollToEnd();

            // 限制日志行数，避免内存泄漏
            if (txtLog.LineCount > 500)
            {
                // 删除最旧的一行
                txtLog.Text = txtLog.Text.Substring(txtLog.Text.IndexOf('\n') + 1);
            }
        }

        /// <summary>
        /// 更新接收计数
        /// </summary>
        private void UpdateRecvCount(int bytes)
        {
            // 简化处理，实际应该用累加器
            lblRecvCount.Content = $"接收: +{bytes} 字节";
        }

        /// <summary>
        /// 窗口关闭时释放资源
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
            base.OnClosing(e);
        }
    }
}