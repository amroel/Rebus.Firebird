using Rebus.Firebird.FirebirdSql;

namespace Rebus.Firebird.Tests;

[TestFixture]
public class RetrierTests
{
	[Test]
	public async Task RetriesUntilCountOfRequestedDelays()
	{
		Retrier retrier = new([TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)]);
		var executionCount = 0;

		await retrier.ExecuteAsync(() =>
		{
			executionCount++;
			return executionCount < 2 ? throw new RandomUnluckyException() : Task.CompletedTask;
		}, _ => { });

		Assert.That(executionCount, Is.EqualTo(2));
	}

	[Test]
	public async Task DoesNotRetryWhenExecutionIsSuccessfulOnFirstTime()
	{
		Retrier retrier = new([TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)]);
		var executionCount = 0;

		await retrier.ExecuteAsync(() =>
		{
			executionCount++;
			return Task.CompletedTask;
		}, _ => { });

		Assert.That(executionCount, Is.EqualTo(1));
	}

	[Test]
	public async Task CallsTheRetryAttemptFunctionStartingFromSecondRetry()
	{
		Retrier retrier = new([TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)]);
		var executionCount = 0;
		var retryAttempts = 0;

		await retrier.ExecuteAsync(() =>
		{
			executionCount++;
			return executionCount < 2 ? throw new RandomUnluckyException() : Task.CompletedTask;
		}, attempt => retryAttempts = attempt);

		Assert.Multiple(() =>
		{
			Assert.That(executionCount, Is.EqualTo(2));
			Assert.That(retryAttempts, Is.EqualTo(1));
		});
	}

	[Test]
	public async Task DoesNotCallRetryAttemptFunctionIfFirstTimeExecutionIsSuccessful()
	{
		Retrier retrier = new([TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)]);
		var executionCount = 0;
		var retryAttempts = -1; // -1 because the function might be called with an index of 0

		await retrier.ExecuteAsync(() =>
		{
			executionCount++;
			return Task.CompletedTask;
		}, attempt => retryAttempts = attempt);

		Assert.Multiple(() =>
		{
			Assert.That(executionCount, Is.EqualTo(1));
			Assert.That(retryAttempts, Is.EqualTo(-1));
		});
	}
}