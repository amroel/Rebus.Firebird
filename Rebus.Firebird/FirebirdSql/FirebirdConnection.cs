using FirebirdSql.Data.FirebirdClient;

namespace Rebus.Firebird.FirebirdSql;
/// <summary>
/// Wraps an opened <see cref="FbConnection"/> and makes it easier to work with
/// </summary>
public sealed class FirebirdConnection : IDisposable
{
	private readonly FbConnection _currentConnection;
	private FbTransaction? _currentTransaction;
	private readonly bool _managedExternally;
	private bool _disposed;

	/// <summary>
	/// Constructs the wrapper with the given connection and transaction
	/// </summary>
	public FirebirdConnection(FbConnection currentConnection,
		FbTransaction currentTransaction,
		bool managedExternally = false)
	{
		_currentConnection = currentConnection ?? throw new ArgumentNullException(nameof(currentConnection));
		_currentTransaction = currentTransaction ?? throw new ArgumentNullException(nameof(currentTransaction));
		_managedExternally = managedExternally;
	}

	/// <summary>
	/// Constructs the wrapper with the given connection which should already be enlisted in a transaction.
	/// </summary>
	public FirebirdConnection(FbConnection currentConnection, bool managedExternally = false)
	{
		_currentConnection = currentConnection ?? throw new ArgumentNullException(nameof(currentConnection));
		_managedExternally = managedExternally;
	}

	public void Dispose()
	{
		if (_managedExternally)
			return;
		if (_disposed)
			return;

		try
		{
			try
			{
				if (_currentTransaction is null)
					return;
				using (_currentTransaction)
				{
					try
					{
						_currentTransaction.Rollback();
					}
					catch { }
					_currentTransaction = null;
				}
			}
			finally
			{
				_currentConnection.Dispose();
			}
		}
		finally
		{
			_disposed = true;
		}
	}

	/// <summary>
	/// Creates a new command, enlisting it in the current transaction
	/// </summary>
	public FbCommand CreateCommand()
	{
		FbCommand command = _currentConnection.CreateCommand();
		command.Transaction = _currentTransaction;
		return command;
	}

	/// <summary>
	/// Completes the transaction
	/// </summary>

	public async Task Complete()
	{
		if (_managedExternally)
			return;
		if (_currentTransaction is null)
			return;
		await using (_currentTransaction)
		{
			await _currentTransaction.CommitAsync();
			_currentTransaction = null;
		}
	}
}
