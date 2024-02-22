using FirebirdSql.Data.FirebirdClient;

namespace Rebus.Firebird;
public static class FbConnectionExt
{
	private static readonly string[] tableName = ["RDB$RELATION_NAME"];

	/// <summary>
	/// Gets the names of all tables in the current database
	/// </summary>
	public static List<TableName> GetTableNames(this FbConnection connection, FbTransaction? transaction = null)
	{
		List<TableName> results = [];
		using FbCommand command = connection.CreateCommand();
		if (transaction != null)
		{
			command.Transaction = transaction;
		}
		command.CommandText = "select rdb$relation_name from rdb$relations where rdb$system_flag = 0 and rdb$view_blr is null";
		using FbDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			results.Add(new TableName(reader.GetString(0)));
		}
		return results;
	}

	/// <summary>
	/// Gets the names of all tables in the current database
	/// </summary>
	public static Dictionary<string, FbDbType> GetColumns(this FbConnection connection,
		string tableName,
		FbTransaction? transaction = null)
	{
		Dictionary<string, FbDbType> results = [];

		using FbCommand command = connection.CreateCommand();
		if (transaction is not null)
		{
			command.Transaction = transaction;
		}

		command.CommandText = """
			select
			    rf.rdb$field_name,
			    case f.rdb$field_type
			        when 7 then
			          case f.rdb$field_sub_type
			            when 0 then 'SMALLINT'
			            when 1 then 'NUMERIC(' || f.rdb$field_precision || ', ' || (-f.rdb$field_scale) || ')'
			            when 2 then 'DECIMAL'
			          end
			        when 8 then
			          case f.rdb$field_sub_type
			            when 0 then 'INTEGER'
			            when 1 then 'NUMERIC('  || f.rdb$field_precision || ', ' || (-f.rdb$field_scale) || ')'
			            when 2 then 'DECIMAL'
			          end
			        when 9 then 'QUAD'
			        when 10 then 'FLOAT'
			        when 12 then 'DATE'
			        when 13 then 'TIME'
			        when 14 then 'CHAR(' || (trunc(f.rdb$field_length / ch.rdb$bytes_per_character)) || ') '
			        when 16 then
			          case f.rdb$field_sub_type
			            when 0 then 'BIGINT'
			            when 1 then 'NUMERIC(' || f.rdb$field_precision || ', ' || (-f.rdb$field_scale) || ')'
			            when 2 then 'DECIMAL'
			          end
			        when 27 then 'DOUBLE'
			        when 35 then 'TIMESTAMP'
			        when 37 then 'VARCHAR(' || (trunc(f.rdb$field_length / ch.rdb$bytes_per_character)) || ')'
			        when 40 then 'CSTRING' || (trunc(f.rdb$field_length / ch.rdb$bytes_per_character)) || ')'
			        when 45 then 'BLOB_ID'
			        when 261 then 'BLOB SUB_TYPE ' || f.rdb$field_sub_type
			        else 'rdb$field_type: ' || f.rdb$field_type || '?'
			    end field_type
			from rdb$relation_fields rf
			join rdb$fields f on f.rdb$field_name = rf.rdb$field_source
			left outer join rdb$character_sets ch on (ch.rdb$character_set_id = f.rdb$character_set_id)
			where
			    rdb$relation_name = @tableName
			""";
		command.Parameters.AddWithValue("@tableName", tableName);
		using FbDataReader reader = command.ExecuteReader();
		while (reader.Read())
		{
			var name = (string)reader["field_name"];
			var typeString = (string)reader["field_type"];
			FbDbType type = GetDbType(typeString);

			results[name] = type;
		}

		return results;
	}

	private static FbDbType GetDbType(string typeString)
	{
		try
		{
			return (FbDbType)Enum.Parse(typeof(FbDbType), typeString, true);
		}
		catch (Exception exception)
		{
			throw new FormatException($"Could not parse '{typeString}' into {typeof(FbDbType)}", exception);
		}
	}
}
