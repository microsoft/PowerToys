using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace Community.PowerToys.Run.Plugin.EnhancedShell
{
    public class ShellCommand
    {
        public int Id { get; set; }
        public string Command { get; set; }
        public ShellType ShellType { get; set; }
        public DateTime ExecutedAt { get; set; }
        public int ExitCode { get; set; }
        public int ExecutionCount { get; set; }
    }

    public class CommandHistory : IDisposable
    {
        private readonly string _connectionString;
        private bool _disposed;

        public CommandHistory(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var createTableQuery = @"
                CREATE TABLE IF NOT EXISTS CommandHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Command TEXT NOT NULL,
                    ShellType INTEGER NOT NULL,
                    ExecutedAt DATETIME NOT NULL,
                    ExitCode INTEGER NOT NULL,
                    ExecutionCount INTEGER DEFAULT 1
                );

                CREATE INDEX IF NOT EXISTS idx_command ON CommandHistory(Command);
                CREATE INDEX IF NOT EXISTS idx_executed_at ON CommandHistory(ExecutedAt DESC);
            ";

            using var command = new SQLiteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        public void Add(ShellCommand command)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Check if command already exists
            var checkQuery = "SELECT Id, ExecutionCount FROM CommandHistory WHERE Command = @Command";
            using (var checkCommand = new SQLiteCommand(checkQuery, connection))
            {
                checkCommand.Parameters.AddWithValue("@Command", command.Command);
                using var reader = checkCommand.ExecuteReader();

                if (reader.Read())
                {
                    // Update existing command
                    var existingId = reader.GetInt32(0);
                    var executionCount = reader.GetInt32(1);

                    var updateQuery = @"
                        UPDATE CommandHistory 
                        SET ExecutedAt = @ExecutedAt, 
                            ExitCode = @ExitCode,
                            ExecutionCount = @ExecutionCount
                        WHERE Id = @Id
                    ";

                    using var updateCommand = new SQLiteCommand(updateQuery, connection);
                    updateCommand.Parameters.AddWithValue("@ExecutedAt", command.ExecutedAt);
                    updateCommand.Parameters.AddWithValue("@ExitCode", command.ExitCode);
                    updateCommand.Parameters.AddWithValue("@ExecutionCount", executionCount + 1);
                    updateCommand.Parameters.AddWithValue("@Id", existingId);
                    updateCommand.ExecuteNonQuery();
                    return;
                }
            }

            // Insert new command
            var insertQuery = @"
                INSERT INTO CommandHistory (Command, ShellType, ExecutedAt, ExitCode)
                VALUES (@Command, @ShellType, @ExecutedAt, @ExitCode)
            ";

            using var insertCommand = new SQLiteCommand(insertQuery, connection);
            insertCommand.Parameters.AddWithValue("@Command", command.Command);
            insertCommand.Parameters.AddWithValue("@ShellType", (int)command.ShellType);
            insertCommand.Parameters.AddWithValue("@ExecutedAt", command.ExecutedAt);
            insertCommand.Parameters.AddWithValue("@ExitCode", command.ExitCode);
            insertCommand.ExecuteNonQuery();
        }

        public List<ShellCommand> GetRecent(int count)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var query = @"
                SELECT Id, Command, ShellType, ExecutedAt, ExitCode, ExecutionCount
                FROM CommandHistory
                ORDER BY ExecutedAt DESC
                LIMIT @Count
            ";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@Count", count);

            return ExecuteQuery(command);
        }

        public List<ShellCommand> Search(string searchTerm)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var query = @"
                SELECT Id, Command, ShellType, ExecutedAt, ExitCode, ExecutionCount
                FROM CommandHistory
                WHERE Command LIKE @SearchTerm
                ORDER BY ExecutionCount DESC, ExecutedAt DESC
                LIMIT 10
            ";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@SearchTerm", $"%{searchTerm}%");

            return ExecuteQuery(command);
        }

        private List<ShellCommand> ExecuteQuery(SQLiteCommand command)
        {
            var results = new List<ShellCommand>();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new ShellCommand
                {
                    Id = reader.GetInt32(0),
                    Command = reader.GetString(1),
                    ShellType = (ShellType)reader.GetInt32(2),
                    ExecutedAt = reader.GetDateTime(3),
                    ExitCode = reader.GetInt32(4),
                    ExecutionCount = reader.GetInt32(5)
                });
            }

            return results;
        }

        public void ClearHistory()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var query = "DELETE FROM CommandHistory";
            using var command = new SQLiteCommand(query, connection);
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            if (_disposed) return;
            SQLiteConnection.ClearAllPools();
            _disposed = true;
        }
    }
}
