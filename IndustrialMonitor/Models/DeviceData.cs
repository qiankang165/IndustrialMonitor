using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace IndustrialMonitor.Models
{
    public class DeviceData
    {
        public int DeviceId { get; set; }          // 设备地址 1、2、3
        public string? DeviceName { get; set; }     // 设备名称
        public ObservableCollection<RegisterData> Registers { get; set; } = new ObservableCollection<RegisterData>();
    }
}
