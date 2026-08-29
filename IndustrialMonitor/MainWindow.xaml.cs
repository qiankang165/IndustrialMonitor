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
        public ObservableCollection<RegisterData> RegisterValues { get; set; } = new ObservableCollection<RegisterData>();

        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            RefreshPortList();
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

                // 构造要发送的数据（不含CRC）
                byte[] command = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x03 };

                // 自动计算CRC
                byte[] crc = Helpers.ModbusCRC.Calculate(command);

                // 拼接完整指令：原数据 + CRC
                byte[] fullCommand = new byte[command.Length + crc.Length];
                Array.Copy(command, 0, fullCommand, 0, command.Length);
                Array.Copy(crc, 0, fullCommand, command.Length, crc.Length);

                // 发送
                _serialPort.Write(fullCommand, 0, fullCommand.Length);
                AppendLog($"📤 发送指令: {BitConverter.ToString(fullCommand).Replace("-", " ")}");


                btnConnect.Content = "关闭串口";
                btnConnect.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(231, 76, 60));
                lblStatus.Content = $"🟢 已连接 {portName}";
                lblStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(46, 204, 113));
                AppendLog($"成功打开串口 {portName}");
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

                    if (read >= 5 && buffer[0] == 0x01 && buffer[1] == 0x03)
                    {
                        int dataLength = buffer[2];
                        int registerCount = dataLength / 2;

                        AppendLog($"📊 解析结果（共 {registerCount} 个寄存器）:");

                        // 1. 先保存当前界面上显示的旧值（用于比对）
                        var oldValues = RegisterValues.ToDictionary(d => d.Address, d => d.Value);

                        // 2. 清空并重新填充新数据
                        RegisterValues.Clear();

                        for (int i = 0; i < registerCount; i++)
                        {
                            int value = (buffer[3 + i * 2] << 8) | buffer[4 + i * 2];

                            // 检查是否变化
                            bool changed = false;
                            if (oldValues.TryGetValue(i, out int oldVal))
                            {
                                if (oldVal != value)
                                {
                                    changed = true;
                                    AppendLog($"   🔔 寄存器 {i} 变化: {oldVal} → {value}");
                                }
                            }
                            else
                            {
                                // 首次读取，不标记为变化
                                changed = false;
                            }

                            RegisterValues.Add(new RegisterData
                            {
                                Address = i,
                                Value = value,
                                HasChanged = changed
                            });
                        }

                        // 3. 如果有任何变化，2秒后清除高亮
                        bool anyChanged = RegisterValues.Any(d => d.HasChanged);
                        if (anyChanged)
                        {
                            var timer = new System.Timers.Timer(2000);
                            timer.Elapsed += (s, ev) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    foreach (var item in RegisterValues)
                                    {
                                        item.HasChanged = false;
                                    }
                                });
                                timer.Stop();
                                timer.Dispose();
                            };
                            timer.AutoReset = false;
                            timer.Start();
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


        private void BtnRead_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                AppendLog("⚠️ 请先打开串口");
                return;
            }

            // 构建读取指令：读取地址0开始的3个寄存器
            byte[] command = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x03 };
            byte[] crc = Helpers.ModbusCRC.Calculate(command);
            byte[] fullCommand = new byte[command.Length + crc.Length];
            Array.Copy(command, 0, fullCommand, 0, command.Length);
            Array.Copy(crc, 0, fullCommand, command.Length, crc.Length);

            _serialPort.Write(fullCommand, 0, fullCommand.Length);
            AppendLog($"📤 发送读取指令: {BitConverter.ToString(fullCommand).Replace("-", " ")}");
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