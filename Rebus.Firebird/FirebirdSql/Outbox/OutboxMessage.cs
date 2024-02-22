using Rebus.Messages;

namespace Rebus.Firebird.FirebirdSql.Outbox;

/// <summary>
/// Represents one single message to be delivered to the transport
/// </summary>
public sealed record OutboxMessage(long Id, string DestinationAddress, Dictionary<string, string> Headers, byte[] Body)
{
	/// <summary>
	/// Gets the <see cref="Headers"/> and <see cref="Body"/> wrapped in a <see cref="TransportMessage"/>
	/// </summary>
	public TransportMessage ToTransportMessage() => new(Headers, Body);
}