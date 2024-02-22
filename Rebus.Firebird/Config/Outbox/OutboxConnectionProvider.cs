using FirebirdSql.Data.FirebirdClient;
using Rebus.Firebird.FirebirdSql.Outbox;

namespace Rebus.Config.Outbox;

internal class OutboxConnectionProvider(string connectionString) : IProvideOutboxConnection
{
	private readonly string _connectionString = connectionString;

	public OutboxConnection GetDbConnection()
	{
		FbConnection connection = new(_connectionString);

		try
		{
			connection.Open();

			FbTransaction transaction = connection.BeginTransaction();

			return new OutboxConnection(connection, transaction);
		}
		catch
		{
			connection.Dispose();
			throw;
		}
	}

	public OutboxConnection GetDbConnectionWithoutTransaction()
	{
		FbConnection connection = new(_connectionString);

		try
		{
			connection.Open();

			return new OutboxConnection(connection);
		}
		catch
		{
			connection.Dispose();
			throw;
		}
	}
}