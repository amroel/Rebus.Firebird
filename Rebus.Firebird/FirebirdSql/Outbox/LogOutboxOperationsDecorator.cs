using Rebus.Logging;

namespace Rebus.Firebird.FirebirdSql.Outbox;
internal sealed class LogOutboxOperationsDecorator(ILog outboxLogger, IReportOutboxOperations inner) :
	IReportOutboxOperations
{
	private readonly ILog _outboxLogger = outboxLogger;
	private readonly IReportOutboxOperations _inner = inner;

	public void ReportSending(int count)
	{
		_outboxLogger.Debug("Sending {0} messages from outbox", count);
		_inner.ReportSending(count);
	}

	public void ReportRetrying(int attempt)
	{
		_outboxLogger.Warn("Retrying to send messages from outbox - attempt {0}", attempt);
		_inner.ReportRetrying(attempt);
	}

	public void ReportSendingFailed(int count)
	{
		_outboxLogger.Warn("Failed to send {0} messages from outbox", count);
		_inner.ReportSendingFailed(count);
	}

	public void ReportSent(int count)
	{
		_outboxLogger.Debug("Sent {0} messages from outbox", count);
		_inner.ReportSent(count);
	}
}
