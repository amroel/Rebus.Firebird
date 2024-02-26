namespace Rebus.Firebird.FirebirdSql;
/// <summary>
/// Firebird database connection provider that allows for easily changing how the current <see cref="FirebirdConnection"/> is obtained,
/// possibly also changing how transactions are handled
/// </summary>
public interface IProvideFirebirdConnection
{
	/// <summary>
	/// Gets a wrapper with the current <see cref="FirebirdConnection"/> inside
	/// </summary>
	Task<FirebirdConnection> GetConnection();
}
