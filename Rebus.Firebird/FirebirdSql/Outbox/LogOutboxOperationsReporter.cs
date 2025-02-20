using Rebus.Logging;

namespace Rebus.Firebird.FirebirdSql.Outbox;

internal sealed class LogOutboxOperationsReporter(ILog outboxLogger) : IReportOutboxOperations
{
	private readonly ILog _outboxLogger = outboxLogger;

	public void ReportSending(int count) => _outboxLogger.Debug("Sending {0} messages from outbox", count);

	public void ReportRetrying(int attempt)
		=> _outboxLogger.Warn("Retrying to send messages from outbox - attempt {0}", attempt);

	public void ReportSendingFailed(int count) => _outboxLogger.Warn("Failed to send {0} messages from outbox", count);

	public void ReportSent(int count) => _outboxLogger.Debug("Sent {0} messages from outbox", count);
}