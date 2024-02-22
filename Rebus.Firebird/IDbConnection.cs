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
	/// Gets the names of all the tables in the current database
	/// </summary>
	IEnumerable<TableName> GetTableNames();

	/// <summary>
	/// Marks that all work has been successfully done and the <see cref="FbConnection"/> may have its transaction committed or whatever is natural to do at this time
	/// </summary>
	Task Complete();

	/// <summary>
	/// Gets information about the columns in the table given by [<paramref name="dataTableName"/>]
	/// </summary>
	IEnumerable<DbColumn> GetColumns(string dataTableName);
}

/// <summary>
/// Represents a Firebird Sql column
/// </summary>
public sealed record DbColumn(string Name, FbDbType Type);
