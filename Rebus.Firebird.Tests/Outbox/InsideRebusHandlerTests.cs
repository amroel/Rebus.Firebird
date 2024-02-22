using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Config.Outbox;
using Rebus.Pipeline;
using Rebus.Routing;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Transport;
using Rebus.Transport.InMem;

namespace Rebus.Firebird.Tests.Outbox;

[TestFixture]
public class InsideRebusHandlerTests : FixtureBase
{
	private static string ConnectionString => FbTestHelper.ConnectionString;
	private InMemNetwork _network = new();

	protected override void SetUp()
	{
		base.SetUp();

		FbTestHelper.DropTable("RebusOutbox");

		_network = new InMemNetwork();
	}

	private record SomeMessage;

	private record AnotherMessage;

	[Test]
	public async Task CanHandleMessageAndSendOutgoingMessagesEvenWhenTransportIsFlaky()
	{
		using ManualResetEvent gotSomeMessage = new(initialState: false);
		using ManualResetEvent gotAnotherMessage = new(initialState: false);

		FlakySenderTransportDecoratorSettings flakySenderTransportDecoratorSettings = new();

		async Task HandlerFunction(IBus bus, IMessageContext context, SomeMessage message)
		{
			await bus.Advanced.Routing.Send("secondConsumer", new AnotherMessage());

			gotSomeMessage.Set();
		}

		using IBus firstConsumer = CreateConsumer("firstConsumer",
			activator => activator.Handle<SomeMessage>(HandlerFunction),
			flakySenderTransportDecoratorSettings);
		using IBus secondConsumer = CreateConsumer("secondConsumer",
			activator => activator.Handle<AnotherMessage>(async _ => gotAnotherMessage.Set()));

		using var client = CreateOneWayClient(router => router.TypeBased().Map<SomeMessage>("firstConsumer"));

		// make it so that the first consumer cannot send
		flakySenderTransportDecoratorSettings.SuccessRate = 0;

		await client.Send(new SomeMessage());

		// wait for SomeMessage to be handled
		gotSomeMessage.WaitOrDie(timeout: TimeSpan.FromSeconds(3));

		// now make it possible for first consumer to send again
		flakySenderTransportDecoratorSettings.SuccessRate = 1;

		// wait for AnotherMessage to arrive
		gotAnotherMessage.WaitOrDie(timeout: TimeSpan.FromSeconds(15));
	}

	private IBus CreateConsumer(string queueName,
		Action<BuiltinHandlerActivator>? handlers = null,
		FlakySenderTransportDecoratorSettings? flakySenderTransportDecoratorSettings = null)
	{
		BuiltinHandlerActivator activator = new();

		handlers?.Invoke(activator);

		Configure.With(activator)
			.Transport(t =>
			{
				t.UseInMemoryTransport(_network, queueName);

				if (flakySenderTransportDecoratorSettings != null)
				{
					t.Decorate(c => new FlakySenderTransportDecorator(c.Get<ITransport>(),
						flakySenderTransportDecoratorSettings));
				}
			})
			.Outbox(o => o.StoreInFirebird(ConnectionString, "RebusOutbox"))
			.Start();

		return activator.Bus;
	}

	private IBus CreateOneWayClient(Action<StandardConfigurer<IRouter>>? routing = null)
		=> Configure.With(new BuiltinHandlerActivator())
			.Transport(t => t.UseInMemoryTransportAsOneWayClient(_network))
			.Routing(r => routing?.Invoke(r))
			.Start();
}
