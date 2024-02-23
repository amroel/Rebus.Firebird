namespace Rebus.Firebird;

/// <summary>
/// Represents a table name in Firebird Sql
/// </summary>
public sealed record TableName
{
	public TableName(string tableName) => Name = StripQuotes(tableName).ToUpperInvariant();

	public string Name { get; }

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
