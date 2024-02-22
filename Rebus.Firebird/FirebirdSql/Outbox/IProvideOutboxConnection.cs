namespace Rebus.Firebird.FirebirdSql.Outbox;

internal interface IProvideOutboxConnection
{
	OutboxConnection GetDbConnection();
	OutboxConnection GetDbConnectionWithoutTransaction();
}