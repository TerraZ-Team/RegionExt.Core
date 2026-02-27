using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using TShockAPI.Configuration;
using TShockAPI.DB;
using TShockAPI.DB.Queries;

namespace RegionExtension.Database
{
    public interface IDatabaseRepository
    {
        string StorageType { get; }
        IDbConnection CreateConnection(TShockSettings settings, string savePath);
    }

    public sealed class DatabaseRepositoryFactory
    {
        private readonly Dictionary<string, IDatabaseRepository> _repositories;

        public DatabaseRepositoryFactory(IEnumerable<IDatabaseRepository> repositories = null)
        {
            repositories ??= new IDatabaseRepository[]
            {
                new SqliteDatabaseRepository(),
                new MySqlDatabaseRepository(),
                new PostgresDatabaseRepository()
            };

            _repositories = new Dictionary<string, IDatabaseRepository>(StringComparer.OrdinalIgnoreCase);
            foreach (var repository in repositories)
            {
                _repositories[NormalizeStorageType(repository.StorageType)] = repository;
            }
        }

        public IDbConnection CreateConnection(TShockSettings settings, string savePath)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var storageType = NormalizeStorageType(settings.StorageType);
            if (!_repositories.TryGetValue(storageType, out var repository))
                throw new NotSupportedException($"Unsupported storage type '{settings.StorageType}'. Supported: sqlite, mysql, postgresql.");

            return repository.CreateConnection(settings, savePath);
        }

        private static string NormalizeStorageType(string storageType)
        {
            if (string.IsNullOrWhiteSpace(storageType))
                return string.Empty;

            var normalized = storageType.Trim().ToLowerInvariant();
            return normalized switch
            {
                "postgresql" => "postgres",
                _ => normalized
            };
        }
    }

    public sealed class SqliteDatabaseRepository : IDatabaseRepository
    {
        public string StorageType => "sqlite";

        public IDbConnection CreateConnection(TShockSettings settings, string savePath)
        {
            if (!string.IsNullOrWhiteSpace(settings.SqliteConnectionString))
                return new SqliteConnection(settings.SqliteConnectionString);

            var filePath = Path.Combine(savePath, "RegionExtension.sqlite");
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            return new SqliteConnection($"Data Source={filePath}");
        }
    }

    public sealed class MySqlDatabaseRepository : IDatabaseRepository
    {
        public string StorageType => "mysql";

        public IDbConnection CreateConnection(TShockSettings settings, string savePath)
        {
            if (!string.IsNullOrWhiteSpace(settings.MySqlConnectionString))
                return new MySqlConnection(settings.MySqlConnectionString);

            var (host, port) = StorageConnectionParser.ParseHostAndPort(settings.MySqlHost, 3306);
            var builder = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = Convert.ToUInt32(port, CultureInfo.InvariantCulture),
                Database = settings.MySqlDbName,
                UserID = settings.MySqlUsername,
                Password = settings.MySqlPassword
            };

            return new MySqlConnection(builder.ConnectionString);
        }
    }

    public sealed class PostgresDatabaseRepository : IDatabaseRepository
    {
        public string StorageType => "postgres";

        public IDbConnection CreateConnection(TShockSettings settings, string savePath)
        {
            if (!string.IsNullOrWhiteSpace(settings.PostgresConnectionString))
                return new NpgsqlConnection(settings.PostgresConnectionString);

            var (host, port) = StorageConnectionParser.ParseHostAndPort(settings.PostgresHost, 5432);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = settings.PostgresDbName,
                Username = settings.PostgresUsername,
                Password = settings.PostgresPassword
            };

            return new NpgsqlConnection(builder.ConnectionString);
        }
    }

    internal static class QueryBuilderFactory
    {
        public static IQueryBuilder Create(IDbConnection connection)
        {
            var sqlType = connection.GetSqlType().ToString().ToLowerInvariant();
            return sqlType switch
            {
                "sqlite" => new SqliteQueryBuilder(),
                "postgres" => new PostgresQueryBuilder(),
                "postgresql" => new PostgresQueryBuilder(),
                _ => new MysqlQueryBuilder()
            };
        }
    }

    internal static class StorageConnectionParser
    {
        public static (string host, int port) ParseHostAndPort(string hostWithPort, int defaultPort)
        {
            if (string.IsNullOrWhiteSpace(hostWithPort))
                return ("localhost", defaultPort);

            var value = hostWithPort.Trim();
            if (value.StartsWith("[", StringComparison.Ordinal) && value.Contains("]:", StringComparison.Ordinal))
            {
                var separatorIndex = value.LastIndexOf("]:", StringComparison.Ordinal);
                var portString = value[(separatorIndex + 2)..];
                if (int.TryParse(portString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
                    return (value[..(separatorIndex + 1)], port);
            }
            else if (value.Count(c => c == ':') == 1)
            {
                var separatorIndex = value.LastIndexOf(':');
                var portString = value[(separatorIndex + 1)..];
                if (int.TryParse(portString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
                    return (value[..separatorIndex], port);
            }

            return (value, defaultPort);
        }

    }
}
