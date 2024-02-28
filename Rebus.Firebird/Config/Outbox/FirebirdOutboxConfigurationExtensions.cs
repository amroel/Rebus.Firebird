using Rebus.Firebird.FirebirdSql.Outbox;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Send;
using Rebus.Retry.Simple;
using Rebus.Threading;
using Rebus.Transport;

namespace Rebus.Config.Outbox;

/// <summary>
/// Configuration extensions for the outbox support
/// </summary>
public static class FirebirdOutboxConfigurationExtensions
{
	/// <summary>
	/// Configures Rebus to use an outbox.
	/// This will store a (message ID, source queue) tuple for all processed messages, 
	/// and under this tuple any messages sent/published will also be stored, 
	/// thus enabling truly idempotent message processing.
	/// </summary>
	public static RebusConfigurer Outbox(this RebusConfigurer configurer,
		Action<StandardConfigurer<IOutboxStorage>> configure,
		Func<bool, int, Task>? whenSuccessOrError = default)
	{
		configurer.Options(o =>
		{
			configure(StandardConfigurer<IOutboxStorage>.GetConfigurerFrom(o));

			// if no outbox storage was registered, no further calls must have been made...
			// that's ok, so we just bail out here
			if (!o.Has<IOutboxStorage>())
				return;

			o.Decorate<ITransport>(c
				=> new OutboxClientTransportDecorator(c.Get<ITransport>(), c.Get<IOutboxStorage>()));

			o.Register(c =>
			{
				IAsyncTaskFactory asyncTaskFactory = c.Get<IAsyncTaskFactory>();
				IRebusLoggerFactory rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
				IOutboxStorage outboxStorage = c.Get<IOutboxStorage>();
				ITransport transport = c.Get<ITransport>();
				return new OutboxForwarder(asyncTaskFactory, rebusLoggerFactory, outboxStorage, transport, whenSuccessOrError);
			});

			o.Decorate(c =>
			{
				_ = c.Get<OutboxForwarder>();
				return c.Get<Options>();
			});

			o.Decorate<IPipeline>(c =>
			{
				IPipeline pipeline = c.Get<IPipeline>();
				IProvideOutboxConnection outboxConnectionProvider = c.Get<IProvideOutboxConnection>();
				OutboxIncomingStep step = new(outboxConnectionProvider);
				return new PipelineStepInjector(pipeline)
					.OnReceive(step, PipelineRelativePosition.After, typeof(DefaultRetryStep));
			});

			o.Decorate<IPipeline>(c =>
			{
				IPipeline pipeline = c.Get<IPipeline>();
				IProvideOutboxConnection outboxConnectionProvider = c.Get<IProvideOutboxConnection>();
				OutboxOutgoingStep step = new(outboxConnectionProvider);
				return new PipelineStepInjector(pipeline)
					.OnSend(step, PipelineRelativePosition.Before, typeof(SendOutgoingMessageStep));
			});
		});

		return configurer;
	}
}
