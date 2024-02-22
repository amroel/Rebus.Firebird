namespace Rebus.Firebird.Tests.Outbox;

internal class RandomUnluckyException : ApplicationException
{
	public RandomUnluckyException() : base("You were unfortunate")
	{
	}
}