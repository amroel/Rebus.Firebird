using Rebus.Bus;
using Rebus.Threading;
using Rebus.Transport;

namespace Rebus.Firebird.FirebirdSql.Outbox;

internal sealed class OutboxForwarder : IDisposable, IInitializable
{
	private static readonly Retrier SendRetrier = new(
	[
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
	]);
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly IOutboxStorage _outboxStorage;
	private readonly ITransport _transport;
	private readonly IAsyncTask _forwarder;
	private readonly IReportOutboxOperations _reporter;

	public OutboxForwarder(IAsyncTaskFactory asyncTaskFactory,
		IOutboxStorage outboxStorage,
		ITransport transport,
		IReportOutboxOperations reporter)
	{
		ArgumentNullException.ThrowIfNull(asyncTaskFactory);
		_outboxStorage = outboxStorage;
		_transport = transport;
		_forwarder = asyncTaskFactory.Create("OutboxForwarder", RunForwarder, intervalSeconds: 1);
		_reporter = reporter;
	}

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_forwarder?.Dispose();
		_cancellationTokenSource?.Dispose();
	}

	public void Initialize() => _forwarder.Start();

	private async Task RunForwarder()
	{
		CancellationToken cancellationToken = _cancellationTokenSource.Token;

		while (!cancellationToken.IsCancellationRequested)
		{
			using OutboxMessageBatch batch = await _outboxStorage.GetNextMessageBatch();

			if (batch.Count == 0)
			{
				return;
			}

			await ProcessMessageBatch(batch, cancellationToken);

			await batch.Complete();
		}
	}

	private async Task ProcessMessageBatch(OutboxMessageBatch batch, CancellationToken cancellationToken)
	{
		_reporter.ReportSending(batch.Count);

		using RebusTransactionScope scope = new();

		foreach (OutboxMessage message in batch)
		{
			var destinationAddress = message.DestinationAddress;
			Messages.TransportMessage transportMessage = message.ToTransportMessage();
			ITransactionContext transactionContext = scope.TransactionContext;

			Task SendMessage() => _transport.Send(destinationAddress, transportMessage, transactionContext);

			await SendRetrier.ExecuteAsync(SendMessage, _reporter.ReportRetrying, cancellationToken);
		}

		try
		{
			await scope.CompleteAsync();
		}
		catch (Exception)
		{
			_reporter.ReportSendingFailed(batch.Count);
			throw;
		}

		_reporter.ReportSent(batch.Count);
	}
}
