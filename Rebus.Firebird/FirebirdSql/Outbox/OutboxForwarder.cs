using Rebus.Bus;
using Rebus.Logging;
using Rebus.Threading;
using Rebus.Transport;

namespace Rebus.Firebird.FirebirdSql.Outbox;

internal class OutboxForwarder : IDisposable, IInitializable
{
	private static readonly Retrier SendRetrier = new(new[]
	{
		TimeSpan.FromSeconds(0.1),
		TimeSpan.FromSeconds(0.1),
		TimeSpan.FromSeconds(0.1),
		TimeSpan.FromSeconds(0.1),
		TimeSpan.FromSeconds(0.1),
		TimeSpan.FromSeconds(0.5),
		TimeSpan.FromSeconds(0.5),
		TimeSpan.FromSeconds(0.5),
		TimeSpan.FromSeconds(0.5),
		TimeSpan.FromSeconds(0.5),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(1),
	});
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly IOutboxStorage _outboxStorage;
	private readonly ITransport _transport;
	private readonly IAsyncTask _forwarder;
	private readonly ILog _logger;

	public OutboxForwarder(IAsyncTaskFactory asyncTaskFactory,
		IRebusLoggerFactory rebusLoggerFactory,
		IOutboxStorage outboxStorage,
		ITransport transport)
	{
		ArgumentNullException.ThrowIfNull(asyncTaskFactory);
		_outboxStorage = outboxStorage;
		_transport = transport;
		_forwarder = asyncTaskFactory.Create("OutboxForwarder", RunForwarder, intervalSeconds: 1);
		_logger = rebusLoggerFactory.GetLogger<OutboxForwarder>();
	}

	public void Initialize() => _forwarder.Start();

	private async Task RunForwarder()
	{
		_logger.Debug("Checking outbox storage for pending messages");

		CancellationToken cancellationToken = _cancellationTokenSource.Token;

		while (!cancellationToken.IsCancellationRequested)
		{
			using OutboxMessageBatch batch = await _outboxStorage.GetNextMessageBatch();

			if (batch.Count == 0)
			{
				_logger.Debug("No pending messages found");
				return;
			}

			await ProcessMessageBatch(batch, cancellationToken);

			await batch.Complete();
		}
	}

	private async Task ProcessMessageBatch(IReadOnlyCollection<OutboxMessage> batch, CancellationToken cancellationToken)
	{
		_logger.Debug("Sending {count} pending messages", batch.Count);

		using RebusTransactionScope scope = new();

		foreach (OutboxMessage message in batch)
		{
			var destinationAddress = message.DestinationAddress;
			Messages.TransportMessage transportMessage = message.ToTransportMessage();
			ITransactionContext transactionContext = scope.TransactionContext;

			Task SendMessage() => _transport.Send(destinationAddress, transportMessage, transactionContext);

			await SendRetrier.ExecuteAsync(SendMessage, cancellationToken);
		}

		await scope.CompleteAsync();

		_logger.Debug("Successfully sent {count} messages", batch.Count);
	}

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_forwarder?.Dispose();
		_cancellationTokenSource?.Dispose();
	}
}
