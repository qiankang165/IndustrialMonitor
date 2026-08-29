using System;

namespace IndustrialMonitor.Helpers
{
    public static class ModbusCRC
    {
        /// <summary>
        /// 计算 Modbus RTU 的 CRC16 校验码
        /// </summary>
        /// <param name="data">待计算的数据（不含CRC本身）</param>
        /// <returns>返回2字节校验码（低字节在前）</returns>
        public static byte[] Calculate(byte[] data)
        {
            ushort crc = 0xFFFF;  // 初始值

            foreach (byte b in data)
            {
                crc ^= b;  // 与当前字节异或

                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)  // 最低位为1
                    {
                        crc >>= 1;
                        crc ^= 0xA001;       // 固定多项式
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            // 返回低字节在前
            return new byte[]
            {
                (byte)(crc & 0xFF),     // 低字节
                (byte)((crc >> 8) & 0xFF)  // 高字节
            };
        }

        /// <summary>
        /// 验证收到的数据 CRC 是否正确
        /// </summary>
        /// <param name="data">包含CRC的完整数据帧</param>
        /// <returns>true表示校验通过，false表示校验失败</returns>
        public static bool Verify(byte[] data)
        {
            if (data.Length < 2)
                return false;

            // 取出最后两个字节作为CRC
            byte[] receivedCrc = new byte[] { data[data.Length - 2], data[data.Length - 1] };

            // 去掉CRC后的数据
            byte[] dataWithoutCrc = new byte[data.Length - 2];
            Array.Copy(data, 0, dataWithoutCrc, 0, data.Length - 2);

            // 计算校验码
            byte[] calculatedCrc = Calculate(dataWithoutCrc);

            // 比较是否一致
            return receivedCrc[0] == calculatedCrc[0] && receivedCrc[1] == calculatedCrc[1];
        }
    }
}