namespace Rebus.Firebird.FirebirdSql;

/// <summary>
/// Mini-Polly 🙂
/// </summary>
internal sealed class Retrier(List<TimeSpan> delays)
{
	private readonly List<TimeSpan> _delays = delays;

	public async Task ExecuteAsync(Func<Task> execute,
		Action<int> retryAttempt,
		CancellationToken cancellationToken = default)
	{
		for (var index = 0; index <= _delays.Count; index++)
		{
			try
			{
				if (index > 0)
					retryAttempt(index + 1);
				await execute();
				return;
			}
			catch (Exception)
			{
				if (index == _delays.Count)
				{
					throw;
				}

				TimeSpan delay = _delays[index];

				try
				{
					await Task.Delay(delay, cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					return;
				}
			}
		}
	}
}