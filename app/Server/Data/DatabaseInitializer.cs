using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Data;

public sealed class DatabaseInitializer
{
    private readonly AppPathsOptions _paths;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IOptions<AppPathsOptions> paths,
        IWebHostEnvironment environment,
        ILogger<DatabaseInitializer> logger)
    {
        _paths = paths.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        var databasePath = ResolvePath(_paths.DatabasePath);
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        var scriptsDirectory = ResolvePath(_paths.SqlScriptsPath);
        if (!Directory.Exists(scriptsDirectory))
        {
            throw new DirectoryNotFoundException($"SQL scripts directory not found: {scriptsDirectory}");
        }

        await using var connection = CreateConnection(databasePath);
        await connection.OpenAsync();

        await ExecuteSqlFileAsync(connection, Path.Combine(scriptsDirectory, "001_schema.sql"));

        var itemCount = await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM items;");
        if (itemCount == 0)
        {
            await ExecuteSqlFileAsync(connection, Path.Combine(scriptsDirectory, "002_seed.sql"));
        }
    }

    public SqliteConnection CreateConnection(string? overridePath = null)
    {
        var databasePath = ResolvePath(overridePath ?? _paths.DatabasePath);
        return new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString());
    }

    private async Task ExecuteSqlFileAsync(IDbConnection connection, string path)
    {
        _logger.LogInformation("Executing SQL script {Path}", path);
        var sql = await File.ReadAllTextAsync(path);
        await connection.ExecuteAsync(sql);
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }
}
