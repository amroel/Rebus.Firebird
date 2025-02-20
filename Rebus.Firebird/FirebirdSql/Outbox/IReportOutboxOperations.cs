namespace Rebus.Firebird.FirebirdSql.Outbox;
public interface IReportOutboxOperations
{
	void ReportSending(int count);
	void ReportRetrying(int attempt);
	void ReportSendingFailed(int count);
	void ReportSent(int count);
}
