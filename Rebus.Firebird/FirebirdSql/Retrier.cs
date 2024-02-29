namespace Rebus.Firebird.FirebirdSql;

/// <summary>
/// Mini-Polly 🙂
/// </summary>
internal sealed class Retrier
{
	private readonly List<TimeSpan> _delays;

	public Retrier(IEnumerable<TimeSpan> delays)
	{
		ArgumentNullException.ThrowIfNull(delays);

		_delays = delays.ToList();
	}

	public async Task ExecuteAsync(Func<Task> execute,
		Action<int> retryAttempt,
		CancellationToken cancellationToken = default)
	{
		for (var index = 0; index <= _delays.Count; index++)
		{
			try
			{
				await execute();
				return;
			}
			catch (Exception)
			{
				if (index == _delays.Count)
				{
					throw;
				}

				retryAttempt(index + 1);
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