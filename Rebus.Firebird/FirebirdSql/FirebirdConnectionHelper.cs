using FirebirdSql.Data.FirebirdClient;

namespace Rebus.Firebird.FirebirdSql;

/// <summary>
/// Helps with managing <see cref="FbConnection"/>s
/// </summary>
public class FirebirdConnectionHelper : IProvideFirebirdConnection
{
	private readonly string _connectionString;
	private readonly Action<FbConnection>? _additionalConnectionSetupCallback;

	/// <summary>
	/// Constructs this thingie
	/// </summary>
	public FirebirdConnectionHelper(string connectionString) => _connectionString = connectionString;

	/// <summary>
	/// Constructs this thingie
	/// </summary>
	/// <param name="connectionString">Connection string.</param>
	/// <param name="additionalConnectionSetupCallback">Additional setup to be performed prior to opening each connection. 
	/// Useful for configuring client certificate authentication, as well as set up other callbacks.</param>
	public FirebirdConnectionHelper(string connectionString, Action<FbConnection> additionalConnectionSetupCallback)
	{
		_connectionString = connectionString;
		_additionalConnectionSetupCallback = additionalConnectionSetupCallback;
	}

	/// <summary>
	/// Gets a fresh, open and ready-to-use connection wrapper
	/// </summary>
	public async Task<FirebirdConnection> GetConnection()
	{
		FbConnection connection = new(_connectionString);

		_additionalConnectionSetupCallback?.Invoke(connection);

		await connection.OpenAsync();
		System.Transactions.Transaction? transaction = System.Transactions.Transaction.Current;
		if (transaction is not null)
		{
			connection.EnlistTransaction(transaction);
			return new FirebirdConnection(connection);
		}
		else
		{
			FbTransactionOptions transactionOptions = new()
			{
				TransactionBehavior = FbTransactionBehavior.ReadCommitted | FbTransactionBehavior.Wait,
				WaitTimeout = TimeSpan.FromSeconds(1)
			};
			FbTransaction currentTransaction = connection.BeginTransaction(transactionOptions);
			return new FirebirdConnection(connection, currentTransaction);
		}
	}
}