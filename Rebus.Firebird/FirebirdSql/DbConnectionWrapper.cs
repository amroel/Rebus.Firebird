using FirebirdSql.Data.FirebirdClient;
using Rebus.Exceptions;

namespace Rebus.Firebird.FirebirdSql;

/// <summary>
/// Wrapper of <see cref="FbConnection"/> that allows for either handling <see cref="FbTransaction"/> automatically, 
/// or for handling it externally
/// </summary>
/// <remarks>
/// Constructs the wrapper, wrapping the given connection and transaction. 
/// It must be indicated with <paramref name="managedExternally"/> whether this wrapper
/// should commit/rollback the transaction (depending on whether <see cref="Complete"/> 
/// is called before <see cref="Dispose()"/>), or if the transaction
/// is handled outside of the wrapper
/// </remarks>
public sealed class DbConnectionWrapper(FbConnection connection,
	FbTransaction? currentTransaction,
	bool managedExternally) :
	IDbConnection
{
	private readonly FbConnection _connection = connection;
	private readonly bool _managedExternally = managedExternally;
	private FbTransaction? _currentTransaction = currentTransaction;
	private bool _disposed;

	/// <summary>
	/// Creates a ready to used <see cref="SqlCommand"/>
	/// </summary>
	public FbCommand CreateCommand()
	{
		FbCommand sqlCommand = _connection.CreateCommand();
		sqlCommand.Transaction = _currentTransaction;
		return sqlCommand;
	}

	/// <summary>
	/// finds out whether the table given by [<paramref name="candidate"/>] exists
	/// </summary>
	public bool TableExists(TableName candidate)
	{
		try
		{
			return _connection.TableExists(candidate, _currentTransaction);
		}
		catch (FbException exception)
		{
			throw new RebusApplicationException(exception, "Could not get table names");
		}
	}

	/// <summary>
	/// Marks that all work has been successfully done and the <see cref="FbConnection"/> 
	/// may have its transaction committed or whatever is natural to do at this time
	/// </summary>
	public async Task Complete()
	{
		if (_managedExternally)
			return;

		if (_currentTransaction is not null)
		{
			await using (_currentTransaction)
			{
				await _currentTransaction.CommitAsync();
				_currentTransaction = null;
			}
		}
	}

	/// <summary>
	/// Finishes the transaction and disposes the connection in order to return it to the connection pool. 
	/// If the transaction has not been committed (by calling <see cref="Complete"/>), 
	/// the transaction will be rolled back.
	/// </summary>
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
				if (_currentTransaction is not null)
				{
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
			}
			finally
			{
				_connection.Dispose();
			}
		}
		finally
		{
			_disposed = true;
		}
	}
}