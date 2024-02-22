using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.Firebird.Tests.Outbox;

internal class FlakySenderTransportDecorator(ITransport transport,
	FlakySenderTransportDecoratorSettings flakySenderTransportDecoratorSettings) : ITransport
{
	private readonly ITransport _transport = transport;
	private readonly FlakySenderTransportDecoratorSettings _flakySenderTransportDecoratorSettings = flakySenderTransportDecoratorSettings;

	public void CreateQueue(string address) => _transport.CreateQueue(address);

	public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
		=> Random.Shared.NextDouble() > _flakySenderTransportDecoratorSettings.SuccessRate
			? throw new RandomUnluckyException()
			: _transport.Send(destinationAddress, message, context);

	public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken)
		=> _transport.Receive(context, cancellationToken);

	public string Address => _transport.Address;
}