using Microsoft.Data.Sqlite;
using Shared;
namespace AuthService;

public sealed class ApplicationUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
public sealed class UserRepository(SqliteDatabase db)
{
    public void Initialize() => db.Initialize("""
        CREATE TABLE IF NOT EXISTS Users (
            Id TEXT PRIMARY KEY, Email TEXT NOT NULL UNIQUE,
            PasswordHash TEXT NOT NULL, CreatedAtUtc TEXT NOT NULL
        );
        """);
    public ApplicationUser? FindByEmail(string email)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Email, PasswordHash FROM Users WHERE Email=$email;";
        command.Parameters.AddWithValue("$email", email);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new ApplicationUser
        { Id = reader.GetString(0), Email = reader.GetString(1), PasswordHash = reader.GetString(2) } : null;
    }
    public void Add(ApplicationUser user)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users(Id, Email, PasswordHash, CreatedAtUtc)
            VALUES($id, $email, $hash, $created);
            """;
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$hash", user.PasswordHash);
        command.Parameters.AddWithValue("$created", Infrastructure.UtcNow());
        command.ExecuteNonQuery();
    }
    public void UpdateHash(ApplicationUser user)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET PasswordHash=$hash WHERE Id=$id;";
        command.Parameters.AddWithValue("$hash", user.PasswordHash);
        command.Parameters.AddWithValue("$id", user.Id);
        command.ExecuteNonQuery();
    }
}
