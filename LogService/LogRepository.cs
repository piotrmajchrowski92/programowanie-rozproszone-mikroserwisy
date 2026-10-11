using Shared;

namespace LogService;

public sealed class LogRepository(SqliteDatabase db)
{
    public void Initialize() => db.Initialize("""
        CREATE TABLE IF NOT EXISTS Logs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TimestampUtc TEXT NOT NULL,
            UserId TEXT NOT NULL,
            Service TEXT NOT NULL,
            Action TEXT NOT NULL,
            Details TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Logs_UserId_Id
        ON Logs(UserId, Id);
        """);

    public long Add(AuditEvent input)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO Logs(
                TimestampUtc,
                UserId,
                Service,
                Action,
                Details
            )
            VALUES(
                $time,
                $user,
                $service,
                $action,
                $details
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$time", Infrastructure.UtcNow());

        command.Parameters.AddWithValue("$user", input.UserId);
        command.Parameters.AddWithValue("$service", input.Service);
        command.Parameters.AddWithValue("$action", input.Action);
        command.Parameters.AddWithValue("$details", input.Details);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public List<AuditLog> List(string userId, int take)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                Id,
                TimestampUtc,
                UserId,
                Service,
                Action,
                Details
            FROM Logs
            WHERE UserId = $user
            ORDER BY Id DESC
            LIMIT $take;
            """;

        command.Parameters.AddWithValue("$user", userId);
        command.Parameters.AddWithValue(
            "$take", Math.Clamp(take, 1, 200));

        using var reader = command.ExecuteReader();
        var result = new List<AuditLog>();

        while (reader.Read())
        {
            result.Add(new AuditLog(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)
            ));
        }

        return result;
    }
}