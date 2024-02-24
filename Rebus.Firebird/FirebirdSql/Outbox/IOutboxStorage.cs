using Rebus.Transport;

namespace Rebus.Firebird.FirebirdSql.Outbox;

/// <summary>
/// Outbox abstraction that enables truly idempotent message processing and store-and-forward for outgoing messages
/// </summary>
public interface IOutboxStorage
{
	/// <summary>
	/// Stores the given <paramref name="outgoingMessages"/> using the given <paramref name="dbConnection"/>.
	/// </summary>
	Task Save(IEnumerable<OutgoingTransportMessage> outgoingMessages, IDbConnection dbConnection);

	/// <summary>
	/// Gets the next message batch to be sent, possibly filtered by the given <paramref name="correlationId"/>. 
	/// MIGHT return messages from other send operations in the rare
	/// case where there is a colission between correlation IDs. 
	/// Returns from 0 to <paramref name="maxMessageBatchSize"/> messages in the batch.
	/// </summary>
	Task<OutboxMessageBatch> GetNextMessageBatch(string? correlationId = null, int maxMessageBatchSize = 100);
}