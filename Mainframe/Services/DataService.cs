using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mainframe.Models;

namespace Mainframe.Services;

public class DataService : IDisposable
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mainframe");

    private static readonly string DbFile = Path.Combine(DataDir, "mainframe.db");
    //private static readonly string LegacyJsonFile = Path.Combine(DataDir, "data.json");

    private readonly SqliteConnection _connection;

    public DataService()
    {
        Directory.CreateDirectory(DataDir);

        _connection = new SqliteConnection($"Data Source={DbFile}");
        _connection.Open();

        Execute("PRAGMA journal_mode=WAL");
        Execute("PRAGMA foreign_keys=ON");

        CreateSchema();
    }

    public AppData Load()
    {
        var data = new AppData();

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = 'UserName'";
            var result = cmd.ExecuteScalar();
            if (result is string userName)
                data.UserName = userName;
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Code, Description FROM ChargeCodes";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.ChargeCodes.Add(new ChargeCode
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Code = reader.GetString(1),
                    Description = reader.GetString(2)
                });
            }
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Name, Description FROM Projects";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.Projects.Add(new Project
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    Tasks = []
                });
            }
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, ProjectId, Name, Description FROM ProjectTasks";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var task = new ProjectTask
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(2),
                    Description = reader.GetString(3),
                    Subtasks = []
                };
                var projectId = Guid.Parse(reader.GetString(1));
                var project = data.Projects.FirstOrDefault(p => p.Id == projectId);
                project?.Tasks.Add(task);
            }
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, TaskId, Name, Description FROM Subtasks";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var subtask = new Subtask
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(2),
                    Description = reader.GetString(3)
                };
                var taskId = Guid.Parse(reader.GetString(1));
                foreach (var project in data.Projects)
                {
                    var task = project.Tasks.FirstOrDefault(t => t.Id == taskId);
                    if (task != null)
                    {
                        task.Subtasks.Add(subtask);
                        break;
                    }
                }
            }
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Date, ChargeCodeId, ProjectId, TaskId, SubtaskId, Hours, Notes FROM TimeEntries";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.TimeEntries.Add(new TimeEntry
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Date = DateOnly.Parse(reader.GetString(1)),
                    ChargeCodeId = Guid.Parse(reader.GetString(2)),
                    ProjectId = Guid.Parse(reader.GetString(3)),
                    TaskId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
                    SubtaskId = reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)),
                    Hours = decimal.Parse(reader.GetString(6)),
                    Notes = reader.GetString(7)
                });
            }
        }

        return data;
    }

    public void Save(AppData data)
    {
        using var transaction = _connection.BeginTransaction();

        Execute("DELETE FROM Subtasks", transaction);
        Execute("DELETE FROM ProjectTasks", transaction);
        Execute("DELETE FROM TimeEntries", transaction);
        Execute("DELETE FROM Projects", transaction);
        Execute("DELETE FROM ChargeCodes", transaction);

        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('UserName', @value)";
            cmd.Parameters.AddWithValue("@value", data.UserName);
            cmd.ExecuteNonQuery();
        }

        foreach (var cc in data.ChargeCodes)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO ChargeCodes (Id, Code, Description) VALUES (@id, @code, @desc)";
            cmd.Parameters.AddWithValue("@id", cc.Id.ToString());
            cmd.Parameters.AddWithValue("@code", cc.Code);
            cmd.Parameters.AddWithValue("@desc", cc.Description);
            cmd.ExecuteNonQuery();
        }

        foreach (var project in data.Projects)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO Projects (Id, Name, Description) VALUES (@id, @name, @desc)";
                cmd.Parameters.AddWithValue("@id", project.Id.ToString());
                cmd.Parameters.AddWithValue("@name", project.Name);
                cmd.Parameters.AddWithValue("@desc", project.Description);
                cmd.ExecuteNonQuery();
            }

            foreach (var task in project.Tasks)
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "INSERT INTO ProjectTasks (Id, ProjectId, Name, Description) VALUES (@id, @projectId, @name, @desc)";
                    cmd.Parameters.AddWithValue("@id", task.Id.ToString());
                    cmd.Parameters.AddWithValue("@projectId", project.Id.ToString());
                    cmd.Parameters.AddWithValue("@name", task.Name);
                    cmd.Parameters.AddWithValue("@desc", task.Description);
                    cmd.ExecuteNonQuery();
                }

                foreach (var subtask in task.Subtasks)
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "INSERT INTO Subtasks (Id, TaskId, Name, Description) VALUES (@id, @taskId, @name, @desc)";
                    cmd.Parameters.AddWithValue("@id", subtask.Id.ToString());
                    cmd.Parameters.AddWithValue("@taskId", task.Id.ToString());
                    cmd.Parameters.AddWithValue("@name", subtask.Name);
                    cmd.Parameters.AddWithValue("@desc", subtask.Description);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        foreach (var entry in data.TimeEntries)
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO TimeEntries (Id, Date, ChargeCodeId, ProjectId, TaskId, SubtaskId, Hours, Notes)
                VALUES (@id, @date, @ccId, @projId, @taskId, @subId, @hours, @notes)
                """;
            cmd.Parameters.AddWithValue("@id", entry.Id.ToString());
            cmd.Parameters.AddWithValue("@date", entry.Date.ToString("O"));
            cmd.Parameters.AddWithValue("@ccId", entry.ChargeCodeId.ToString());
            cmd.Parameters.AddWithValue("@projId", entry.ProjectId.ToString());
            cmd.Parameters.AddWithValue("@taskId", entry.TaskId.HasValue ? entry.TaskId.Value.ToString() : DBNull.Value);
            cmd.Parameters.AddWithValue("@subId", entry.SubtaskId.HasValue ? entry.SubtaskId.Value.ToString() : DBNull.Value);
            cmd.Parameters.AddWithValue("@hours", entry.Hours.ToString());
            cmd.Parameters.AddWithValue("@notes", entry.Notes);
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void CreateSchema()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS ChargeCodes (
                Id TEXT PRIMARY KEY,
                Code TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT ''
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Projects (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT ''
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS ProjectTasks (
                Id TEXT PRIMARY KEY,
                ProjectId TEXT NOT NULL REFERENCES Projects(Id),
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT ''
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Subtasks (
                Id TEXT PRIMARY KEY,
                TaskId TEXT NOT NULL REFERENCES ProjectTasks(Id),
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT ''
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS TimeEntries (
                Id TEXT PRIMARY KEY,
                Date TEXT NOT NULL,
                ChargeCodeId TEXT NOT NULL,
                ProjectId TEXT NOT NULL,
                TaskId TEXT,
                SubtaskId TEXT,
                Hours TEXT NOT NULL,
                Notes TEXT NOT NULL DEFAULT ''
            )
            """);

        Execute("CREATE INDEX IF NOT EXISTS IX_TimeEntries_Date ON TimeEntries(Date)");
        Execute("CREATE INDEX IF NOT EXISTS IX_ProjectTasks_ProjectId ON ProjectTasks(ProjectId)");
        Execute("CREATE INDEX IF NOT EXISTS IX_Subtasks_TaskId ON Subtasks(TaskId)");
    }

    private void Execute(string sql, SqliteTransaction? transaction = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
