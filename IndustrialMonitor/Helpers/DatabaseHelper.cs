using Microsoft.Data.Sqlite;
using System;

namespace IndustrialMonitor.Helpers
{
    public class DatabaseHelper
    {
        private static string _connectionString = "Data Source=IndustrialData.db";

        /// <summary>
        /// 初始化数据库表
        /// </summary>
        public static void InitializeDatabase()
        {
            //using的作用是确保在使用完资源后，资源能够被正确释放。在这里，`using`语句用于管理数据库连接和命令对象的生命周期。具体来说：
            // 1. `using var connection = new SqliteConnection(_connectionString);`：创建一个数据库连接对象，并在使用完后自动关闭连接。
            // 2. `using var command = new SqliteCommand(createTableSql, connection);`：创建一个SQL命令对象，并在使用完后自动释放资源。
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS HistoryData (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceId INTEGER NOT NULL,
                    RegisterAddress INTEGER NOT NULL,
                    Value INTEGER NOT NULL,
                    Timestamp TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_device_timestamp 
                    ON HistoryData(DeviceId, Timestamp);
            ";

            using var command = new SqliteCommand(createTableSql, connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 插入一条数据
        /// </summary>
        public static void InsertData(int deviceId, int registerAddress, int value)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string insertSql = @"
                INSERT INTO HistoryData (DeviceId, RegisterAddress, Value, Timestamp)
                VALUES (@DeviceId, @RegisterAddress, @Value, @Timestamp)
            ";

            using var command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@DeviceId", deviceId);
            command.Parameters.AddWithValue("@RegisterAddress", registerAddress);
            command.Parameters.AddWithValue("@Value", value);
            command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 查询某设备某寄存器的历史数据（最近100条）
        /// </summary>
        public static List<HistoryRecord> GetHistoryData(int deviceId, int registerAddress, int limit = 100)
        {
            var result = new List<HistoryRecord>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string querySql = @"
                SELECT Timestamp, Value 
                FROM HistoryData 
                WHERE DeviceId = @DeviceId AND RegisterAddress = @RegisterAddress
                ORDER BY Timestamp DESC
                LIMIT @Limit
            ";

            using var command = new SqliteCommand(querySql, connection);
            command.Parameters.AddWithValue("@DeviceId", deviceId);
            command.Parameters.AddWithValue("@RegisterAddress", registerAddress);
            command.Parameters.AddWithValue("@Limit", limit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new HistoryRecord
                {
                    Timestamp = reader.GetString(0),
                    Value = reader.GetInt32(1)
                });
            }

            // 按时间正序（从旧到新，适合画曲线）
            result.Reverse();
            return result;
        }

        /// <summary>
        /// 清理旧数据（保留最近7天）
        /// </summary>
        public static void CleanOldData(int days = 7)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string deleteSql = @"
                DELETE FROM HistoryData 
                WHERE Timestamp < datetime('now', '-' || @Days || ' days')
            ";

            using var command = new SqliteCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@Days", days);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// 历史记录实体
    /// </summary>
    public class HistoryRecord
    {
        public string Timestamp { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}