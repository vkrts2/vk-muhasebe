namespace ErmayMuhasebe.Data;

public static class Constants
{
    public const string DatabaseFilename = "ErmayV4_Stable.db3";
    private const string DefaultDatabasePassword = "ERMAY-SECURE-DB-KEY-2025-V2";
    public static string DatabasePassword => Environment.GetEnvironmentVariable("ERMAY_DB_KEY") ?? DefaultDatabasePassword;


    public const SQLite.SQLiteOpenFlags Flags =
        SQLite.SQLiteOpenFlags.ReadWrite |
        SQLite.SQLiteOpenFlags.Create;

    private static string? _databasePath;
    public static string DatabasePath
    {
        get
        {
            if (_databasePath != null) return _databasePath;
            
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            
            return Path.Combine(appData, DatabaseFilename);
        }
        set => _databasePath = value;
    }
}
