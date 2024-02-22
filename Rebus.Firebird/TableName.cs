namespace Rebus.Firebird;

/// <summary>
/// Represents a table name in Firebird Sql
/// </summary>
public sealed class TableName(string tableName)
{
	public string Name { get; } = StripQuotes(tableName);

	public override string ToString() => Name;

	private static string StripQuotes(string value)
	{
		if (value.StartsWith('\"'))
		{
			value = value[1..];
		}
		if (value.EndsWith('\"'))
		{
			value = value[..^1];
		}

		return value;
	}
}
