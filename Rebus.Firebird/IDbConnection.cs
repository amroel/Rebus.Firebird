using FirebirdSql.Data.FirebirdClient;

namespace Rebus.Firebird;

/// <summary>
/// Wrapper of <see cref="FbConnection"/> that allows for easily changing how transactions are handled, and possibly how <see cref="FbConnection"/> instances
/// are reused by various services
/// </summary>
public interface IDbConnection : IDisposable
{
	/// <summary>
	/// Creates a ready to used <see cref="FbCommand"/>
	/// </summary>
	FbCommand CreateCommand();

	/// <summary>
	/// finds out whether the table given by [<paramref name="candidate"/>] exists
	/// </summary>
	bool TableExists(TableName candidate);

	/// <summary>
	/// Marks that all work has been successfully done and the <see cref="FbConnection"/> may have its transaction committed or whatever is natural to do at this time
	/// </summary>
	Task Complete();
}