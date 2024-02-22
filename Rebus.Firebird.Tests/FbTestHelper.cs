using DotNet.Testcontainers.Builders;
using FirebirdSql.Data.FirebirdClient;
using Rebus.Exceptions;
using Rebus.Firebird.Internals;
using Rebus.Firebird.Tests.Testcontainers.Firebird;
using Rebus.Tests.Contracts;

namespace Rebus.Firebird.Tests;
internal sealed class FbTestHelper
{
	private const int TableUnknown = 335544580;
	private static FirebirdContainer? firebirdContainer;
	private static readonly FirebirdConnectionHelper FirebirdConnectionHelper = new(ConnectionString);

	private static string? _connectionString;

	public static string DatabaseName => $"rebus2_test_{TestConfig.Suffix}".TrimEnd('_') + ".fdb";

	public static string ConnectionString
	{
		get
		{
			if (_connectionString is not null)
			{
				return _connectionString;
			}

			var databaseName = DatabaseName;

			if (firebirdContainer is null)
			{
				InitializeDatabase(databaseName);
			}

			Console.WriteLine("Using firebird SQL database {0}", databaseName);
			_connectionString = new FbConnectionStringBuilder(firebirdContainer?.GetConnectionString())
			{
				Pooling = false,
				Charset = "UTF8"
			}.ToString();

			return _connectionString;
		}
	}

	public static void DropTable(string table) => AsyncHelper.RunSync(async () =>
		{
			TableName tableName = new(table);
			using FirebirdConnection connection = await FirebirdConnectionHelper.GetConnection();

			await using FbCommand command = connection.CreateCommand();

			command.CommandText = $"drop table {tableName};";

			try
			{
				command.ExecuteNonQuery();

				Console.WriteLine("Dropped firebird table '{0}'", tableName);
			}
			catch (FbException exception) when (exception.Errors.Any(e => e.Number == TableUnknown))
			{
				// it's alright
			}

			await connection.Complete();
		});

	private static void InitializeDatabase(string databaseName)
	{
		try
		{
			firebirdContainer = new FirebirdBuilder()
				.WithImage("jacobalberty/firebird:3.0")
				.WithDatabaseName(databaseName)
				.WithUsername("sysdba")
				.WithPassword("masterkey")
				.EnableLegacyClientAuth()
				.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(FirebirdBuilder.FIREBIRD_PORT))
				.Build();

			AsyncHelper.RunSync(async () => await firebirdContainer.StartAsync());
		}
		catch (Exception exception)
		{
			throw new RebusApplicationException(exception, $"Could not initialize database '{databaseName}'");
		}
	}

}
