using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Npgsql;
using StackExchange.Redis;

namespace Worker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var redisHostname = Environment.GetEnvironmentVariable("ENV_VAR_REDIS_HOSTNAME") ?? "redis";
            var redisPort = Environment.GetEnvironmentVariable("ENV_VAR_REDIS_PORT") ?? "6379";
            var redisPassword = Environment.GetEnvironmentVariable("ENV_VAR_REDIS_PASSWORD") ?? "redis_password";
            var pgHost = Environment.GetEnvironmentVariable("ENV_VAR_POSTGRES_HOST") ?? "db";
            var pgPort = Environment.GetEnvironmentVariable("ENV_VAR_POSTGRES_PORT") ?? "5432";
            var pgDatabase = Environment.GetEnvironmentVariable("ENV_VAR_POSTGRES_DATABASE") ?? "postgres";
            var pgUser = Environment.GetEnvironmentVariable("ENV_VAR_POSTGRES_USER") ?? "postgres_user";
            var pgPassword = Environment.GetEnvironmentVariable("ENV_VAR_POSTGRES_PASSWORD") ?? "postgres_password";
            // Syntax: Server=db;Port=5432;Database=postgres;User Id=postgres_user;Password=postgres_password
            var connectionString = $"Server={pgHost};Port={pgPort};Database={pgDatabase};User Id={pgUser};Password={pgPassword};";
            Console.WriteLine("Using connection string: " + connectionString);

            try
            {                
                var pgsql = OpenDbConnection(connectionString);
                var redisConn = OpenRedisConnection(redisHostname, redisPassword);
                var redis = redisConn.GetDatabase();

                // Keep alive is not implemented in Npgsql yet. This workaround was recommended:
                // https://github.com/npgsql/npgsql/issues/1214#issuecomment-235828359
                var keepAliveCommand = pgsql.CreateCommand();
                keepAliveCommand.CommandText = "SELECT 1";

                var definition = new { vote = "", voter_id = "" };
                while (true)
                {
                    // Reconnect redis if down
                    if (redisConn == null || !redisConn.IsConnected) {
                        Console.WriteLine("Reconnecting Redis");
                        redis = OpenRedisConnection(redisHostname, redisPassword).GetDatabase();
                    }
                    string json = redis.ListLeftPopAsync("votes").Result;
                    if (json != null)
                    {
                        var vote = JsonConvert.DeserializeAnonymousType(json, definition);
                        Console.WriteLine($"Processing vote for '{vote.vote}' by '{vote.voter_id}'");
                        // Reconnect DB if down
                        if (!pgsql.State.Equals(System.Data.ConnectionState.Open))
                        {
                            Console.WriteLine("Reconnecting DB");
                            pgsql = OpenDbConnection(connectionString);
                        }
                        else
                        { // Normal +1 vote requested
                            UpdateVote(pgsql, vote.voter_id, vote.vote);
                        }
                    }
                    else
                    {
                        keepAliveCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static NpgsqlConnection OpenDbConnection(string connectionString)
        {
            NpgsqlConnection connection;

            while (true)
            {
                try
                {                    
                    connection = new NpgsqlConnection(connectionString);
                    connection.Open();
                    break;
                }
                catch (SocketException ex)
                {
                    Console.Error.WriteLine("Waiting for db");
                    Console.Error.WriteLine("Error Message: " + ex.ToString());
                    Thread.Sleep(1000);
                }
                catch (DbException ex)
                {
                    Console.Error.WriteLine("Waiting for db");
                    Console.Error.WriteLine("Error Message: " + ex.ToString());
                    Thread.Sleep(1000);
                }
            }

            Console.WriteLine("Connected to db");

            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS votes (
                                        id VARCHAR(255) NOT NULL UNIQUE,
                                        vote VARCHAR(255) NOT NULL
                                    )";
            command.ExecuteNonQuery();

            return connection;
        }

        private static ConnectionMultiplexer OpenRedisConnection(string redisHostname, string redisPassword)
        {
            // Use IP address to workaround https://github.com/StackExchange/StackExchange.Redis/issues/410
            var ipAddress = GetIp(redisHostname);
            Console.WriteLine($"Found Redis at {ipAddress}");

            while (true)
            {
                try
                {
                    Console.Error.WriteLine("Connecting to Redis");
                    return ConnectionMultiplexer.Connect(ipAddress + ",password=" + redisPassword);
                }
                catch (RedisConnectionException ex)
                {
                    Console.Error.WriteLine("Waiting for Redis");
                    Console.Error.WriteLine("Error Message: " + ex.ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        private static string GetIp(string hostname)
            => Dns.GetHostEntryAsync(hostname)
                .Result
                .AddressList
                .First(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToString();

        private static void UpdateVote(NpgsqlConnection connection, string voterId, string vote)
        {
            var command = connection.CreateCommand();
            try
            {
                command.CommandText = "INSERT INTO votes (id, vote) VALUES (@id, @vote)";
                command.Parameters.AddWithValue("@id", voterId);
                command.Parameters.AddWithValue("@vote", vote);
                command.ExecuteNonQuery();
            }
            catch (DbException)
            {
                command.CommandText = "UPDATE votes SET vote = @vote WHERE id = @id";
                command.ExecuteNonQuery();
            }
            finally
            {
                command.Dispose();
            }
        }
    }
}
