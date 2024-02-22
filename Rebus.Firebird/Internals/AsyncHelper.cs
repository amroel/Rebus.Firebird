using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Rebus.Firebird.Internals;
public static class AsyncHelper
{
	/// <summary>
	/// Executes a task synchronously on the calling thread by installing a temporary synchronization context that queues continuations
	///  </summary>
	public static void RunSync(Func<Task> task)
	{
		SynchronizationContext? currentContext = SynchronizationContext.Current;
		CustomSynchronizationContext customContext = new(task);

		try
		{
			SynchronizationContext.SetSynchronizationContext(customContext);

			customContext.Run();
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(currentContext);
		}
	}

	private sealed class CustomSynchronizationContext(Func<Task> task) : SynchronizationContext, IDisposable
	{
		private readonly ConcurrentQueue<Tuple<SendOrPostCallback, object?>> _items = new();
		private readonly AutoResetEvent _workItemsWaiting = new(false);
		private readonly Func<Task> _task = task ?? throw new ArgumentNullException(nameof(task), "Please remember to pass a Task to be executed");
		private ExceptionDispatchInfo? _caughtException;
		private bool _done;

		public void Dispose() => _workItemsWaiting.Dispose();

		public override void Post(SendOrPostCallback function, object? state)
		{
			_items.Enqueue(Tuple.Create(function, state));
			_workItemsWaiting.Set();
		}

		/// <summary>
		/// Enqueues the function to be executed and executes all resulting continuations until it is completely done
		/// </summary>
		public void Run()
		{
			Post(async _ =>
			{
				try
				{
					await _task();
				}
				catch (Exception exception)
				{
					_caughtException = ExceptionDispatchInfo.Capture(exception);
					throw;
				}
				finally
				{
					Post(_ => _done = true, null);
				}
			}, null);

			while (!_done)
			{
				if (_items.TryDequeue(out Tuple<SendOrPostCallback, object?>? task))
				{
					task.Item1(task.Item2);

					if (_caughtException == null) continue;

					_caughtException.Throw();
				}
				else
				{
					_workItemsWaiting.WaitOne();
				}
			}
		}

		public override void Send(SendOrPostCallback d, object? state)
			=> throw new NotSupportedException("Cannot send to same thread");

		public override SynchronizationContext CreateCopy() => this;
	}
}
