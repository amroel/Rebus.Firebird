using FirebirdSql.Data.FirebirdClient;

namespace Rebus.Firebird.FirebirdSql.Outbox;

/// <summary>
/// Holds an open <see cref="FbConnection"/>
/// </summary>
public sealed class OutboxConnection
{
	/// <summary>
	/// Gets the connection
	/// </summary>
	public FbConnection Connection { get; }

	/// <summary>
	/// Gets the current transaction
	/// </summary>
	public FbTransaction? Transaction { get; }

	internal OutboxConnection(FbConnection connection, FbTransaction? transaction = default)
	{
		Connection = connection;
		Transaction = transaction;
	}
}
