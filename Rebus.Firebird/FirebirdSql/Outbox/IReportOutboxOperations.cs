namespace Rebus.Firebird.FirebirdSql.Outbox;
public interface IReportOutboxOperations
{
	void ReportChecking();
	void ReportNoPendingMessages();
	void ReportSending(int count);
	void ReportRetrying(int attempt);
	void ReportSendingFailed(int count);
	void ReportSent(int count);
}
