namespace Backend.Common;

public enum DatabaseType
{
    Postgresql,
    MySql
}

public static class DatabaseSelected
{
    public static DatabaseType Type { get; set; } = DatabaseType.Postgresql;
}
