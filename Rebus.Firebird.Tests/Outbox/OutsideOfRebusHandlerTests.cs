using System.Transactions;
using FirebirdSql.Data.FirebirdClient;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Config.Outbox;
using Rebus.Routing;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Transport;
using Rebus.Transport.InMem;

namespace Rebus.Firebird.Tests.Outbox;

[TestFixture]
public class OutsideOfRebusHandlerTests : FixtureBase
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

	[Test]
	public async Task CannotUseOutboxTwice()
	{
		await using FbConnection connection = new(ConnectionString);
		await connection.OpenAsync();
		await using FbTransaction transaction = await connection.BeginTransactionAsync();

		using RebusTransactionScope scope = new();
		scope.UseOutbox(connection, transaction);

		Assert.Throws<InvalidOperationException>(() => scope.UseOutbox(connection, transaction));
	}

	[TestCase(true, true)]
	[TestCase(false, false)]
	public async Task CanUseOutboxOutsideOfRebusHandler_Publish(bool commitTransaction, bool expectMessageToBeReceived)
	{
		FlakySenderTransportDecoratorSettings settings = new();

		using ManualResetEvent messageWasReceived = new(initialState: false);
		using IBus server = CreateConsumer("server", a => a.Handle<SomeMessage>(_ =>
		{
			messageWasReceived.Set();
			return Task.CompletedTask;
		}));

		await server.Subscribe<SomeMessage>();

		using IBus client = CreateOneWayClient(flakySenderTransportDecoratorSettings: settings);

		// set success rate pretty low, so we're sure that it's currently not possible to use the
		// real transport - this is a job for the outbox! 
		settings.SuccessRate = 0;

		// pretending we're in a web app - we have these two bad boys at work:
		await using FbConnection connection = new(ConnectionString);
		await connection.OpenAsync();
		await using FbTransaction transaction = await connection.BeginTransactionAsync();

		// this is how we would use the outbox for outgoing messages
		using RebusTransactionScope scope = new();
		scope.UseOutbox(connection, transaction);
		await client.Publish(new SomeMessage());
		await scope.CompleteAsync();

		if (commitTransaction)
		{
			// this is what we were all waiting for!
			await transaction.CommitAsync();
		}

		// we would not have gotten this far without the outbox - now let's pretend that the transport has recovered
		settings.SuccessRate = 1;

		// wait for server to receive the event
		Assert.That(messageWasReceived.WaitOne(TimeSpan.FromSeconds(2)), Is.EqualTo(expectMessageToBeReceived),
			$"When commitTransaction={commitTransaction} we {(expectMessageToBeReceived ? "expected the message to be sent and thus received" : "did NOT expect the message to be sent and therefore also not received")}");
	}

	[TestCase(true, true)]
	[TestCase(false, false)]
	public async Task CanUseOutboxOutsideOfRebusHandler_AmbientTransaction_Publish(bool commitTransaction,
		bool expectMessageToBeReceived)
	{
		FlakySenderTransportDecoratorSettings settings = new();

		using ManualResetEvent messageWasReceived = new(initialState: false);
		using IBus server = CreateConsumer("server", a => a.Handle<SomeMessage>(_ =>
		{
			messageWasReceived.Set();
			return Task.CompletedTask;
		}));

		await server.Subscribe<SomeMessage>();

		using IBus client = CreateOneWayClient(flakySenderTransportDecoratorSettings: settings);

		// set success rate pretty low, so we're sure that it's currently not possible to use the
		// real transport - this is a job for the outbox! 
		settings.SuccessRate = 0;

		// pretending we're in a web app - we have these two bad boys at work:
		using (TransactionScope tx = new(TransactionScopeAsyncFlowOption.Enabled))
		{
			// this is how we would use the outbox for outgoing messages
			await client.Publish(new SomeMessage());

			if (commitTransaction)
			{
				// this is what we were all waiting for!
				tx.Complete();
			}
		}

		// we would not have gotten this far without the outbox - now let's pretend that the transport has recovered
		settings.SuccessRate = 1;

		// wait for server to receive the event
		Assert.That(messageWasReceived.WaitOne(TimeSpan.FromSeconds(2)), Is.EqualTo(expectMessageToBeReceived),
			$"When commitTransaction={commitTransaction} we {(expectMessageToBeReceived ? "expected the message to be sent and thus received" : "did NOT expect the message to be sent and therefore also not received")}");
	}

	[TestCase(true, true)]
	[TestCase(false, false)]
	public async Task CanUseOutboxOutsideOfRebusHandler_Send(bool commitTransaction, bool expectMessageToBeReceived)
	{
		FlakySenderTransportDecoratorSettings settings = new();

		using ManualResetEvent messageWasReceived = new(initialState: false);
		using IBus server = CreateConsumer("server", a => a.Handle<SomeMessage>(_ =>
		{
			messageWasReceived.Set();
			return Task.CompletedTask;
		}));
		using IBus client = CreateOneWayClient(r => r.TypeBased().Map<SomeMessage>("server"), settings);

		// set success rate pretty low, so we're sure that it's currently not possible to use the
		// real transport - this is a job for the outbox! 
		settings.SuccessRate = 0;

		// pretending we're in a web app - we have these two bad boys at work:
		await using (FbConnection connection = new(ConnectionString))
		{
			await connection.OpenAsync();
			await using FbTransaction transaction = await connection.BeginTransactionAsync();

			// this is how we would use the outbox for outgoing messages
			using RebusTransactionScope scope = new();
			scope.UseOutbox(connection, transaction);
			await client.Send(new SomeMessage());
			await scope.CompleteAsync();

			if (commitTransaction)
			{
				// this is what we were all waiting for!
				await transaction.CommitAsync();
			}
		}

		// we would not have gotten this far without the outbox - now let's pretend that the transport has recovered
		settings.SuccessRate = 1;

		// wait for server to receive the event
		Assert.That(messageWasReceived.WaitOne(TimeSpan.FromSeconds(2)), Is.EqualTo(expectMessageToBeReceived),
			$"When commitTransaction={commitTransaction} we {(expectMessageToBeReceived ? "expected the message to be sent and thus received" : "did NOT expect the message to be sent and therefore also not received")}");
	}

	[TestCase(true, true)]
	[TestCase(false, false)]
	public async Task CanUseOutboxOutsideOfRebusHandler_AmbientTransaction_Send(bool commitTransaction,
		bool expectMessageToBeReceived)
	{
		FlakySenderTransportDecoratorSettings settings = new();

		using ManualResetEvent messageWasReceived = new(initialState: false);
		using IBus server = CreateConsumer("server", a => a.Handle<SomeMessage>(_ =>
		{
			messageWasReceived.Set();
			return Task.CompletedTask;
		}));
		using IBus client = CreateOneWayClient(r => r.TypeBased().Map<SomeMessage>("server"), settings);

		// set success rate pretty low, so we're sure that it's currently not possible to use the
		// real transport - this is a job for the outbox! 
		settings.SuccessRate = 0;

		// pretending we're in a web app - we have these two bad boys at work:
		using (TransactionScope tx = new(TransactionScopeAsyncFlowOption.Enabled))
		{

			await client.Send(new SomeMessage());

			if (commitTransaction)
			{
				// this is what we were all waiting for!
				tx.Complete();
			}
		}

		// we would not have gotten this far without the outbox - now let's pretend that the transport has recovered
		settings.SuccessRate = 1;

		// wait for server to receive the event
		Assert.That(messageWasReceived.WaitOne(TimeSpan.FromSeconds(2)), Is.EqualTo(expectMessageToBeReceived),
			$"When commitTransaction={commitTransaction} we {(expectMessageToBeReceived ? "expected the message to be sent and thus received" : "did NOT expect the message to be sent and therefore also not received")}");
	}

	private IBus CreateConsumer(string queueName, Action<BuiltinHandlerActivator>? handlers = null)
	{
		BuiltinHandlerActivator activator = new();

		handlers?.Invoke(activator);

		Configure.With(activator)
			.Transport(t => t.UseInMemoryTransport(_network, queueName))
			.Start();

		return activator.Bus;
	}

	private IBus CreateOneWayClient(Action<StandardConfigurer<IRouter>>? routing = null,
		FlakySenderTransportDecoratorSettings? flakySenderTransportDecoratorSettings = null)
		=> Configure.With(new BuiltinHandlerActivator())
			.Transport(t =>
			{
				t.UseInMemoryTransportAsOneWayClient(_network);

				if (flakySenderTransportDecoratorSettings != null)
				{
					t.Decorate(c => new FlakySenderTransportDecorator(c.Get<ITransport>(),
						flakySenderTransportDecoratorSettings));
				}
			})
			.Routing(r => routing?.Invoke(r))
			.Outbox(o => o.StoreInFirebird(ConnectionString, "RebusOutbox"))
			.Start();
}