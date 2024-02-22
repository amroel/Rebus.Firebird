namespace Rebus.Firebird.Tests.Outbox;

internal class FlakySenderTransportDecoratorSettings
{
	public double SuccessRate { get; set; } = 1;
}