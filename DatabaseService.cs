using Microsoft.Data.Sqlite;

namespace SealScout;

public static class DatabaseService
{
    private static string DbPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SealLead",
            "SealScout.db");

    public static string ConnectionString =>
        $"Data Source={DbPath}";

    public static void Initialize()
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(DbPath)!);

        using var connection =
            new SqliteConnection(ConnectionString);

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS AppUsers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserName TEXT NOT NULL UNIQUE,
    Email TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    LastLoginAt TEXT
);

CREATE TABLE IF NOT EXISTS Searches (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SearchActivity TEXT NOT NULL,
    OriginalUrl TEXT NOT NULL,
    FinalUrl TEXT NOT NULL,
    OnlyWithEmail INTEGER NOT NULL DEFAULT 1,
    ExtractionUserId INTEGER NOT NULL,
    TotalCompanies INTEGER DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'Pendiente',
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FinishedAt TEXT,
    StopReason TEXT,
    FOREIGN KEY (ExtractionUserId) REFERENCES AppUsers(Id)
);

CREATE TABLE IF NOT EXISTS Companies (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CompanyName TEXT,
    Email TEXT,
    Phone TEXT,
    ProfileUrl TEXT UNIQUE,
    Address TEXT,
    LegalName TEXT,
    Cif TEXT,
    LegalForm TEXT,
    Sector TEXT,
    Activity TEXT,
    CnaeActivity TEXT,
    SearchKeywords TEXT,
    EmailStatus TEXT NOT NULL DEFAULT 'Pendiente',
    EmailSentCount INTEGER NOT NULL DEFAULT 0,
    LastEmailSentAt TEXT,
    LastSenderUserId INTEGER,
    CompanyStatus TEXT NOT NULL DEFAULT 'Nueva',
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY (LastSenderUserId) REFERENCES AppUsers(Id)
);

CREATE TABLE IF NOT EXISTS SearchResults (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SearchId INTEGER NOT NULL,
    CompanyId INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SearchId) REFERENCES Searches(Id),
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
    UNIQUE(SearchId, CompanyId)
);

CREATE TABLE IF NOT EXISTS SearchProgress (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SearchId INTEGER NOT NULL UNIQUE,
    CurrentPage INTEGER NOT NULL DEFAULT 1,
    LastCompanyUrl TEXT,
    CurrentLocalityUrl TEXT DEFAULT '',
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SearchId) REFERENCES Searches(Id)
);

CREATE TABLE IF NOT EXISTS EmailHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CompanyId INTEGER NOT NULL,
    SenderUserId INTEGER NOT NULL,
    SentAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Subject TEXT,
    Body TEXT,
    TemplateName TEXT,
    Status TEXT NOT NULL DEFAULT 'Enviado',
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
    FOREIGN KEY (SenderUserId) REFERENCES AppUsers(Id)
);

CREATE TABLE IF NOT EXISTS Notes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CompanyId INTEGER NOT NULL,
    UserId INTEGER NOT NULL,
    NoteText TEXT NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id),
    FOREIGN KEY (UserId) REFERENCES AppUsers(Id)
);

INSERT OR IGNORE INTO AppUsers (
    Id,
    UserName,
    Email,
    PasswordHash,
    IsActive
)
VALUES (
    1,
    'Laura',
    'laura.horna@factum.es',
    'TEMPORAL',
    1
);
";


        command.ExecuteNonQuery();

        // Migración: añadir columna si la BD ya existía sin ella
        try
        {
            var migrate = connection.CreateCommand();
            migrate.CommandText = "ALTER TABLE SearchProgress ADD COLUMN CurrentLocalityUrl TEXT DEFAULT '';";
            migrate.ExecuteNonQuery();
        }
        catch { }
    }
}