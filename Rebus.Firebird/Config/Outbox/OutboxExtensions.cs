using FirebirdSql.Data.FirebirdClient;
using Rebus.Firebird.FirebirdSql;
using Rebus.Firebird.FirebirdSql.Outbox;
using Rebus.Transport;

namespace Rebus.Config.Outbox;

/// <summary>
/// Configuration extensions for Firebird Server-based outbox
/// </summary>
public static class OutboxExtensions
{
	internal const string CurrentOutboxConnectionKey = "current-outbox-connection";

	/// <summary>
	/// Configures Firebird Server as the outbox storage
	/// </summary>
	public static void StoreInFirebird(this StandardConfigurer<IOutboxStorage> configurer,
		string connectionString,
		string senderAddress,
		string tableName) => StoreInFirebird(configurer, connectionString, senderAddress, new TableName(tableName));

	/// <summary>
	/// Configures Firebird Server as the outbox storage
	/// </summary>
	public static void StoreInFirebird(this StandardConfigurer<IOutboxStorage> configurer,
		string connectionString,
		string senderAddress,
		TableName tableName)
	{
		IDbConnection ConnectionProvider(ITransactionContext context)
		{
			// if we find a connection in the context, use that (and accept that its lifestyle is managed somewhere else):
			if (context.Items.TryGetValue(CurrentOutboxConnectionKey, out var result) && result is OutboxConnection outboxConnection)
			{
				return new DbConnectionWrapper(outboxConnection.Connection, outboxConnection.Transaction, managedExternally: true);
			}

			FbConnection connection = new(connectionString);

			connection.Open();

			try
			{
				FbTransaction transaction = connection.BeginTransaction();

				return new DbConnectionWrapper(connection, transaction, managedExternally: false);
			}
			catch
			{
				connection.Dispose();
				throw;
			}
		}

		configurer
			.OtherService<IOutboxStorage>()
			.Register(_ => new FirebirdOutboxStorage(ConnectionProvider, senderAddress, tableName));

		configurer.OtherService<IProvideOutboxConnection>()
			.Register(_ => new OutboxConnectionProvider(connectionString));
	}

	/// <summary>
	/// Enables the use of outbox on the <see cref="RebusTransactionScope"/>. Will enlist all outgoing message operations in the
	/// <paramref name="connection"/>/<paramref name="transaction"/> passed to the method.
	/// </summary>
	public static void UseOutbox(this RebusTransactionScope rebusTransactionScope, FbConnection connection, FbTransaction transaction)
	{
		ITransactionContext context = rebusTransactionScope.TransactionContext;

		if (!context.Items.TryAdd(CurrentOutboxConnectionKey, new OutboxConnection(connection, transaction)))
		{
			throw new InvalidOperationException("Cannot add the given connection/transaction to the current Rebus transaction, because a connection/transaction has already been added!");
		}
	}
}