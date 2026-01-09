using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace RoundEndSounds;

public class DataBaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DataBaseService> _logger;

    public DataBaseService(RoundEndSoundsConfig config)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<DataBaseService>();

        MySqlConnectionStringBuilder builder = new()
        {
            Server = config.DatabaseHost,
            Port = (uint)config.DatabasePort,
            UserID = config.DatabaseUser,
            Password = config.DatabasePassword,
            Database = config.DatabaseName,
            Pooling = true
        };
        _connectionString = builder.ConnectionString;
    }

    public async Task InitDataBaseAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            const string createTableQuery = """

                                                            CREATE TABLE IF NOT EXISTS `res_users` (
                                                                `steamid` BIGINT UNSIGNED PRIMARY KEY,
                                                                `volume` FLOAT NOT NULL DEFAULT 1.0,
                                                                `enabled` TINYINT(1) NOT NULL DEFAULT 1
                                                            );
                                            """;

            await connection.ExecuteAsync(createTableQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database (Create Table)");
        }
    }

    public async Task<Dictionary<ulong, UserSettings>> LoadAllUsers()
    {
        var result = new Dictionary<ulong, UserSettings>();
        const string sql = "SELECT steamid, volume, enabled FROM res_users";

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var rows = await connection.QueryAsync(sql);

            foreach (var row in rows)
            {
                var steamId = (ulong)row.steamid;
                result[steamId] = new UserSettings
                {
                    Volume = Convert.ToSingle(row.volume), Enabled = Convert.ToBoolean(row.enabled)
                };
            }

            _logger.LogInformation("[RoundEndSounds] Loaded {ResultCount} users from database into cache.",
                result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all users from database");
        }

        return result;
    }

    public async Task SaveUser(ulong steamId, UserSettings settings)
    {
        const string sql = """

                                       INSERT INTO res_users (steamid, volume, enabled) 
                                       VALUES (@steamId, @Volume, @Enabled)
                                       ON DUPLICATE KEY UPDATE volume = @Volume, enabled = @Enabled;
                           """;

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync(sql, new
            {
                steamId,
                settings.Volume,
                settings.Enabled
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user {SteamId}", steamId);
        }
    }
}