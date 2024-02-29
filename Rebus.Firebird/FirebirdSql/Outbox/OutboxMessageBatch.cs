using System.Collections;

namespace Rebus.Firebird.FirebirdSql.Outbox;

/// <summary>
/// Wraps a batch of <see cref="OutboxMessage"/>s along with a function that "completes" the batch 
/// (i.e. ensures that it will not be handled again -... e.g. by deleting it, or marking it as completed)
/// </summary>
/// <remarks>
/// Creates the batch
/// </remarks>
public sealed class OutboxMessageBatch(Func<Task> completionFunction,
	IEnumerable<OutboxMessage> messages,
	Action disposeFunction) : IDisposable, IReadOnlyList<OutboxMessage>
{
	/// <summary>
	/// Gets an empty outbox message batch that doesn't complete anything 
	/// and only performs some kind of cleanup when done
	/// </summary>
	public static OutboxMessageBatch Empty(Action disposeFunction)
		=> new(() => Task.CompletedTask, Array.Empty<OutboxMessage>(), disposeFunction);

	private readonly IReadOnlyList<OutboxMessage> _messages = messages.ToList();
	private readonly Func<Task> _completionFunction = completionFunction;
	private readonly Action _disposeFunction = disposeFunction;

	/// <summary>
	/// Marks the message batch as properly handled
	/// </summary>
	public async Task Complete() => await _completionFunction();

	/// <summary>
	/// Performs any cleanup actions necessary
	/// </summary>
	public void Dispose() => _disposeFunction();

	/// <summary>
	/// Gets how many
	/// </summary>
	public int Count => _messages.Count;

	/// <summary>
	/// Gets by index
	/// </summary>
	public OutboxMessage this[int index] => _messages[index];

	/// <summary>
	/// Gets an enumerator for the wrapped sequence of <see cref="OutboxMessage"/>s
	/// </summary>
	public IEnumerator<OutboxMessage> GetEnumerator() => _messages.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}