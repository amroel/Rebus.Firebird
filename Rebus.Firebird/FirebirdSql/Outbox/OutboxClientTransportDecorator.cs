using System.Collections.Concurrent;
using Rebus.Config.Outbox;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.Firebird.FirebirdSql.Outbox;

internal class OutboxClientTransportDecorator(ITransport transport, IOutboxStorage outboxStorage) : ITransport
{
	private const string OutgoingMessagesKey = "outbox-outgoing-messages";
	private readonly ITransport _transport = transport;
	private readonly IOutboxStorage _outboxStorage = outboxStorage;

	public void CreateQueue(string address) => _transport.CreateQueue(address);

	public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
	{
		OutboxConnection connection = context.GetOrNull<OutboxConnection>(OutboxExtensions.CurrentOutboxConnectionKey);

		if (connection is null)
		{
			return _transport.Send(destinationAddress, message, context);
		}

		ConcurrentQueue<OutgoingTransportMessage> outgoingMessages = context.GetOrAdd(OutgoingMessagesKey, () =>
		{
			ConcurrentQueue<OutgoingTransportMessage> queue = new();
			DbConnectionWrapper dbConnectionWrapper = new(connection.Connection,
				connection.Transaction,
				managedExternally: true);
			context.OnCommit(async _ => await _outboxStorage.Save(queue, dbConnectionWrapper));
			return queue;
		});

		outgoingMessages.Enqueue(new OutgoingTransportMessage(message, destinationAddress));

		return Task.CompletedTask;
	}

	public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
		=> _transport.Receive(context, cancellationToken);

	public string Address => _transport.Address;
}