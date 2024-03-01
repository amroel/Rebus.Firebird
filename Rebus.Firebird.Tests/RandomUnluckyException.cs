namespace Rebus.Firebird.Tests;

internal sealed class RandomUnluckyException : ApplicationException
{
	public RandomUnluckyException() : base("You were unfortunate")
	{
	}
}