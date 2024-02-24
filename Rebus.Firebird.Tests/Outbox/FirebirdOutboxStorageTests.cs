using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Rebus.Firebird.FirebirdSql.Outbox;
using Rebus.Messages;
using Rebus.Tests.Contracts;
using Rebus.Transport;

namespace Rebus.Firebird.Tests.Outbox;

[TestFixture]
public class FirebirdOutboxStorageTests : FixtureBase
{
	private FirebirdOutboxStorage _storage = new(GetNewDbConnection, new("silly"));

	protected override void SetUp()
	{
		base.SetUp();

		const string tableName = "Outbox";
		FbTestHelper.DropTable(tableName);

		_storage = new FirebirdOutboxStorage(GetNewDbConnection, new TableName(tableName));
		_storage.Initialize();
	}

	[Test]
	public async Task CanStoreBatchOfMessages_RoundTrip()
	{
		TransportMessage transportMessage = new([], [1, 2, 3]);
		OutgoingTransportMessage outgoingMessage = new(transportMessage, "wherever");

		using RebusTransactionScope scope = new();
		using DbConnectionWrapper connection = GetNewDbConnection(scope.TransactionContext);

		await _storage.Save(new[] { outgoingMessage }, connection);

		await connection.Complete();
		await scope.CompleteAsync();

		using OutboxMessageBatch outboxMessageBatch = await _storage.GetNextMessageBatch();

		Assert.That(outboxMessageBatch, Has.Count.EqualTo(1));
		OutboxMessage outboxMessage = outboxMessageBatch[0];
		Assert.Multiple(() =>
		{
			Assert.That(outboxMessage.DestinationAddress, Is.EqualTo("wherever"));
			Assert.That(outboxMessage.Body, Is.EqualTo(new byte[] { 1, 2, 3 }));
		});
	}

	[TestCase(true)]
	[TestCase(false)]
	public async Task CanStoreBatchOfMessages_ManagedExternally(bool commitAndExpectTheMessagesToBeThere)
	{
		await using FbConnection connection = new(FbTestHelper.ConnectionString);
		await connection.OpenAsync();

		await using FbTransaction transaction = await connection.BeginTransactionAsync();

		DbConnectionWrapper dbConnection = new(connection, transaction, managedExternally: true);

		TransportMessage transportMessage = new([], [1, 2, 3]);
		OutgoingTransportMessage outgoingMessage = new(transportMessage, "wherever");

		await _storage.Save(new[] { outgoingMessage }, dbConnection);

		if (commitAndExpectTheMessagesToBeThere)
		{
			await transaction.CommitAsync();
		}

		if (commitAndExpectTheMessagesToBeThere)
		{
			using OutboxMessageBatch batch1 = await _storage.GetNextMessageBatch();
			await batch1.Complete();

			using OutboxMessageBatch batch2 = await _storage.GetNextMessageBatch();

			Assert.Multiple(() =>
			{
				Assert.That(batch1, Has.Count.EqualTo(1));
				Assert.That(batch2, Is.Empty);
			});
		}
		else
		{
			using OutboxMessageBatch batch = await _storage.GetNextMessageBatch();

			Assert.That(batch, Is.Empty);
		}
	}

	[Test]
	public async Task CanStoreBatchOfMessages_Complete()
	{
		TransportMessage transportMessage = new([], [1, 2, 3]);
		OutgoingTransportMessage outgoingMessage = new(transportMessage, "wherever");

		using RebusTransactionScope scope = new();
		using DbConnectionWrapper connection = GetNewDbConnection(scope.TransactionContext);

		await _storage.Save(new[] { outgoingMessage }, connection);

		await connection.Complete();
		await scope.CompleteAsync();

		using OutboxMessageBatch batch1 = await _storage.GetNextMessageBatch();
		await batch1.Complete();

		using OutboxMessageBatch batch2 = await _storage.GetNextMessageBatch();

		Assert.Multiple(() =>
		{
			Assert.That(batch1, Has.Count.EqualTo(1));
			Assert.That(batch2, Is.Empty);
		});
	}

	[Test]
	public async Task CanGetBatchesOfMessages_VaryingBatchSize()
	{
		static OutgoingTransportMessage CreateOutgoingMessage(string body)
		{
			TransportMessage transportMessage = new([], Encoding.UTF8.GetBytes(body));
			OutgoingTransportMessage outgoingMessage1 = new(transportMessage, "wherever");
			return outgoingMessage1;
		}
		using RebusTransactionScope scope = new();
		using DbConnectionWrapper connection = GetNewDbConnection(scope.TransactionContext);

		List<string> texts = Enumerable.Range(0, 100).Select(n => $"message {n:000}").ToList();
		await _storage.Save(texts.Select(CreateOutgoingMessage), connection);

		await connection.Complete();
		await scope.CompleteAsync();

		using (OutboxMessageBatch batch1 = await _storage.GetNextMessageBatch(maxMessageBatchSize: 10))
			Assert.That(batch1, Has.Count.EqualTo(10));

		using (OutboxMessageBatch batch2 = await _storage.GetNextMessageBatch(maxMessageBatchSize: 12))
			Assert.That(batch2, Has.Count.EqualTo(12));

		using (OutboxMessageBatch batch3 = await _storage.GetNextMessageBatch(maxMessageBatchSize: 77))
			Assert.That(batch3, Has.Count.EqualTo(77));

		using OutboxMessageBatch batch4 = await _storage.GetNextMessageBatch(maxMessageBatchSize: 1);
		Assert.That(batch4, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task CanGetBatchesOfMessages_TwoBatchesInParallel()
	{
		static OutgoingTransportMessage CreateOutgoingMessage(string body)
		{
			TransportMessage transportMessage = new([], Encoding.UTF8.GetBytes(body));
			OutgoingTransportMessage outgoingMessage1 = new(transportMessage, "wherever");
			return outgoingMessage1;
		}
		using RebusTransactionScope scope = new();
		using DbConnectionWrapper connection = GetNewDbConnection(scope.TransactionContext);

		List<string> texts = Enumerable.Range(0, 200).Select(n => $"message {n:000}").ToList();
		await _storage.Save(texts.Select(CreateOutgoingMessage), connection);

		await connection.Complete();
		await scope.CompleteAsync();

		using OutboxMessageBatch batch1 = await _storage.GetNextMessageBatch();
		await batch1.Complete();
		Assert.That(batch1, Has.Count.EqualTo(100));

		using OutboxMessageBatch batch2 = await _storage.GetNextMessageBatch();
		await batch2.Complete();
		Assert.That(batch2, Has.Count.EqualTo(100));

		List<string> roundtrippedTexts = batch1.Concat(batch2).Select(b => Encoding.UTF8.GetString(b.Body)).ToList();

		Assert.That(roundtrippedTexts.OrderBy(t => t), Is.EqualTo(texts));
	}

	private static DbConnectionWrapper GetNewDbConnection(ITransactionContext _)
	{
		FbConnection connection = new(FbTestHelper.ConnectionString);
		connection.Open();
		FbTransaction transaction = connection.BeginTransaction();
		return new DbConnectionWrapper(connection, transaction, managedExternally: false);
	}
}