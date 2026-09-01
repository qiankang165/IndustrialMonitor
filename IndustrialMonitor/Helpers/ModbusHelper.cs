using System;

namespace IndustrialMonitor.Helpers
{
    public static class ModbusHelper
    {
        /// <summary>
        /// 构造 Modbus 06 写入单寄存器指令
        /// </summary>
        /// <param name="deviceId">设备地址 (1~247)</param>
        /// <param name="registerAddress">寄存器地址 (0~65535)</param>
        /// <param name="value">要写入的值 (0~65535)</param>
        /// <returns>完整的指令字节数组（含CRC）</returns>
        public static byte[] BuildWriteSingleRegisterCommand(byte deviceId, ushort registerAddress, ushort value)
        {
            // Modbus 06 帧结构：设备地址 + 功能码(0x06) + 寄存器地址(2字节) + 值(2字节) + CRC(2字节)
            byte[] command = new byte[6];
            command[0] = deviceId;                          // 设备地址
            command[1] = 0x06;                              // 功能码：写单个寄存器
            command[2] = (byte)(registerAddress >> 8);      // 寄存器地址高字节
            command[3] = (byte)(registerAddress & 0xFF);    // 寄存器地址低字节
            command[4] = (byte)(value >> 8);                // 值高字节
            command[5] = (byte)(value & 0xFF);              // 值低字节

            // 计算 CRC
            byte[] crc = ModbusCRC.Calculate(command);
            byte[] fullCommand = new byte[command.Length + crc.Length];
            Array.Copy(command, 0, fullCommand, 0, command.Length);
            Array.Copy(crc, 0, fullCommand, command.Length, crc.Length);

            return fullCommand;
        }

        /// <summary>
        /// 验证写寄存器响应是否正确
        /// </summary>
        public static bool IsWriteResponseValid(byte[] response, byte deviceId, ushort registerAddress, ushort value)
        {
            if (response.Length < 8) return false;
            if (response[0] != deviceId) return false;
            if (response[1] != 0x06) return false;
            if (response[2] != (registerAddress >> 8)) return false;
            if (response[3] != (registerAddress & 0xFF)) return false;
            if (response[4] != (value >> 8)) return false;
            if (response[5] != (value & 0xFF)) return false;
            return true;
        }
    }
}