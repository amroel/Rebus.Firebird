using FirebirdSql.Data.FirebirdClient;

namespace Rebus.Firebird.FirebirdSql;
public static class FbConnectionExt
{
	public static bool TableExists(this FbConnection connection, TableName candidate, FbTransaction? transaction = null)
	{
		using FbCommand command = connection.CreateCommand();
		if (transaction != null)
		{
			command.Transaction = transaction;
		}
		command.CommandText = """
			select 
				rdb$relation_name 
			from rdb$relations
			where 
				rdb$system_flag = 0 
				and rdb$view_blr is null
				and rdb$relation_name = @tableName
			""";
		command.Parameters.AddWithValue("@tableName", candidate.ToString());
		using FbDataReader reader = command.ExecuteReader();
		return reader.Read();
	}
}
