using Rebus.Config.Outbox;
using Rebus.Pipeline;
using Rebus.Transport;

namespace Rebus.Firebird.FirebirdSql.Outbox;

internal sealed class OutboxIncomingStep(IProvideOutboxConnection outboxConnectionProvider) : IIncomingStep
{
	private readonly IProvideOutboxConnection _outboxConnectionProvider = outboxConnectionProvider
		?? throw new ArgumentNullException(nameof(outboxConnectionProvider));

	public async Task Process(IncomingStepContext context, Func<Task> next)
	{
		OutboxConnection outboxConnection = _outboxConnectionProvider.GetDbConnection();
		ITransactionContext transactionContext = context.Load<ITransactionContext>();

		transactionContext.Items[OutboxExtensions.CurrentOutboxConnectionKey] = outboxConnection;

		transactionContext.OnCommit(async _ => await outboxConnection.Transaction.CommitAsync());

		transactionContext.OnDisposed(_ =>
		{
			outboxConnection.Transaction.Dispose();
			outboxConnection.Connection.Dispose();
		});

		await next();
	}
}