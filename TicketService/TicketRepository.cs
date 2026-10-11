using Microsoft.Data.Sqlite;
using Shared;
namespace TicketService;

public sealed class TicketRepository(SqliteDatabase db)
{
    public void Initialize() => db.Initialize("""
        CREATE TABLE IF NOT EXISTS Tickets (
            Id INTEGER PRIMARY KEY AUTOINCREMENT, OwnerId TEXT NOT NULL,
            Title TEXT NOT NULL, Description TEXT NOT NULL, Status TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_Tickets_OwnerId ON Tickets(OwnerId);
        """);
    private static Ticket Read(SqliteDataReader r) => new(r.GetInt32(0), r.GetString(1),
        r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6));
    public List<Ticket> All(string owner)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Tickets WHERE OwnerId=$owner ORDER BY Id DESC;";
        command.Parameters.AddWithValue("$owner", owner);
        using var reader = command.ExecuteReader();
        var result = new List<Ticket>();
        while (reader.Read()) result.Add(Read(reader));
        return result;
    }
    public Ticket? Find(int id, string owner)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Tickets WHERE Id=$id AND OwnerId=$owner;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", owner);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }
    public Ticket Add(string owner, TicketInput input)
    {
        var now = Infrastructure.UtcNow();
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tickets(OwnerId,Title,Description,Status,CreatedAtUtc,UpdatedAtUtc)
            VALUES($owner,$title,$description,$status,$now,$now);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$owner", owner);
        command.Parameters.AddWithValue("$title", input.Title);
        command.Parameters.AddWithValue("$description", input.Description ?? "");
        command.Parameters.AddWithValue("$status", input.Status);
        command.Parameters.AddWithValue("$now", now);
        var id = Convert.ToInt32(command.ExecuteScalar());
        return new Ticket(id, owner, input.Title, input.Description ?? "", input.Status, now, now);
    }
    public Ticket? Update(int id, string owner, TicketInput input)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Tickets SET Title=$title,Description=$description,Status=$status,UpdatedAtUtc=$now
            WHERE Id=$id AND OwnerId=$owner;
            """;
        command.Parameters.AddWithValue("$title", input.Title);
        command.Parameters.AddWithValue("$description", input.Description ?? "");
        command.Parameters.AddWithValue("$status", input.Status);
        command.Parameters.AddWithValue("$now", Infrastructure.UtcNow());
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", owner);
        return command.ExecuteNonQuery() == 0 ? null : Find(id, owner);
    }
    public bool Delete(int id, string owner)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tickets WHERE Id=$id AND OwnerId=$owner;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", owner);
        return command.ExecuteNonQuery() > 0;
    }
}
