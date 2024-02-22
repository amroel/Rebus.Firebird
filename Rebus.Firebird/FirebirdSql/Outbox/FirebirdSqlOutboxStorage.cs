﻿using FirebirdSql.Data.FirebirdClient;
using Rebus.Bus;
using Rebus.Firebird.Internals;
using Rebus.Serialization;
using Rebus.Transport;

namespace Rebus.Firebird.FirebirdSql.Outbox;

/// <summary>
/// Outbox implementation that uses a table in Firebird sql Server to store the necessary outbox information
/// </summary>
/// <remarks>
/// Creates the outbox storage
/// </remarks>
public class FirebirdSqlOutboxStorage(Func<ITransactionContext, IDbConnection> connectionProvider, TableName tableName)
	: IOutboxStorage, IInitializable
{
	private static readonly HeaderSerializer HeaderSerializer = new();
	private readonly Func<ITransactionContext, IDbConnection> _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
	private readonly TableName _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));

	/// <summary>
	/// Initializes the outbox storage
	/// </summary>
	public void Initialize()
	{
		async Task InitializeAsync()
		{
			using RebusTransactionScope scope = new();
			using IDbConnection connection = _connectionProvider(scope.TransactionContext);

			if (connection.GetTableNames().Contains(_tableName)) return;

			try
			{
				using FbCommand command = connection.CreateCommand();

				command.CommandText = $"""
					CREATE TABLE {_tableName}
					(
					    "Id" BIGINT GENERATED BY DEFAULT AS IDENTITY,
					    "CorrelationId" VARCHAR(16) NULL,
					    "MessageId" VARCHAR(255) NULL,
					    "SourceQueue" VARCHAR(255) NULL,
					    "DestinationAddress" VARCHAR(255) NOT NULL,
					    "Headers" TEXT NULL,
					    "Body" BYTEA NULL,
					    "Sent" BOOLEAN NOT NULL DEFAULT(FALSE),
					    PRIMARY KEY ("Id")
					)
					""";

				await command.ExecuteNonQueryAsync();

				await connection.Complete();
			}
			catch (Exception)
			{
				if (!connection.GetTableNames().Contains(_tableName))
				{
					throw;
				}
			}

			await scope.CompleteAsync();
		}

		AsyncHelper.RunSync(InitializeAsync);
	}

	/// <summary>
	/// Stores the given <paramref name="outgoingMessages"/> as being the result of processing message with ID <paramref name="messageId"/>
	/// in the queue of this particular endpoint. If <paramref name="outgoingMessages"/> is an empty sequence, a note is made of the fact
	/// that the message with ID <paramref name="messageId"/> has been processed.
	/// </summary>
	public async Task Save(IEnumerable<OutgoingTransportMessage> outgoingMessages, string messageId = null, string sourceQueue = null, string correlationId = null)
	{
		if (outgoingMessages == null) throw new ArgumentNullException(nameof(outgoingMessages));

		await InnerSave(outgoingMessages, messageId, sourceQueue, correlationId);
	}

	/// <summary>
	/// Stores the given <paramref name="outgoingMessages"/> using the given <paramref name="dbConnection"/>.
	/// </summary>
	public async Task Save(IEnumerable<OutgoingTransportMessage> outgoingMessages, IDbConnection dbConnection)
	{
		if (outgoingMessages == null) throw new ArgumentNullException(nameof(outgoingMessages));
		if (dbConnection == null) throw new ArgumentNullException(nameof(dbConnection));

		await SaveUsingConnection(dbConnection, outgoingMessages);
	}

	/// <inheritdoc />
	public async Task<OutboxMessageBatch> GetNextMessageBatch(string correlationId = null, int maxMessageBatchSize = 100)
	{
		return await InnerGetMessageBatch(maxMessageBatchSize, correlationId);
	}

	private async Task<OutboxMessageBatch> InnerGetMessageBatch(int maxMessageBatchSize, string correlationId)
	{
		if (maxMessageBatchSize <= 0)
		{
			throw new ArgumentException(
				$"Cannot retrieve {maxMessageBatchSize} messages - please pass in a value >= 1",
				nameof(maxMessageBatchSize));
		}

		// no 'using' here, because this will be passed to the outbox message batch
		RebusTransactionScope scope = new();

		try
		{
			// no 'using' here either, because this will be passed to the outbox message batch
			IDbConnection connection = _connectionProvider(scope.TransactionContext);

			// this must be done when cleanining up
			void Dispose()
			{
				connection.Dispose();
				scope.Dispose();
			}

			try
			{
				List<OutboxMessage> messages = await GetOutboxMessages(connection, maxMessageBatchSize, correlationId);

				// bail out if no messages were found
				if (!messages.Any()) return OutboxMessageBatch.Empty(Dispose);

				// define what it means to complete the batch
				async Task Complete()
				{
					await connection.Complete();
					await scope.CompleteAsync();
				}

				return new OutboxMessageBatch(Complete, messages, Dispose);
			}
			catch (Exception)
			{
				connection.Dispose();
				throw;
			}
		}
		catch (Exception)
		{
			scope.Dispose();
			throw;
		}
	}

	private async Task InnerSave(IEnumerable<OutgoingTransportMessage> outgoingMessages, string messageId, string sourceQueue, string correlationId)
	{
		using RebusTransactionScope scope = new();
		using IDbConnection connection = _connectionProvider(scope.TransactionContext);

		await SaveUsingConnection(connection, outgoingMessages, messageId, sourceQueue, correlationId);

		await connection.Complete();
		await scope.CompleteAsync();
	}

	private async Task SaveUsingConnection(IDbConnection connection, IEnumerable<OutgoingTransportMessage> outgoingMessages, string messageId = null, string sourceQueue = null, string correlationId = null)
	{
		foreach (OutgoingTransportMessage message in outgoingMessages)
		{
			using global::FirebirdSql.Data.FirebirdClient.FbCommand command = connection.CreateCommand();

			Messages.TransportMessage transportMessage = message.TransportMessage;
			var body = message.TransportMessage.Body;
			var headers = SerializeHeaders(transportMessage.Headers);

			command.CommandText = $"INSERT INTO {_tableName} (\"CorrelationId\", \"MessageId\", \"SourceQueue\", \"DestinationAddress\", \"Headers\", \"Body\") VALUES (@correlationId, @messageId, @sourceQueue, @destinationAddress, @headers, @body)";
			command.Parameters.Add("correlationId", FbDbType.VarChar, 16).Value = (object)correlationId ?? DBNull.Value;
			command.Parameters.Add("messageId", FbDbType.VarChar, 255).Value = (object)messageId ?? DBNull.Value;
			command.Parameters.Add("sourceQueue", FbDbType.VarChar, 255).Value = (object)sourceQueue ?? DBNull.Value;
			command.Parameters.Add("destinationAddress", FbDbType.VarChar, 255).Value = message.DestinationAddress;
			command.Parameters.Add("headers", FbDbType.VarChar, headers.Length.RoundUpToNextPowerOfTwo()).Value = headers;
			command.Parameters.Add("body", FbDbType.Binary, body.Length.RoundUpToNextPowerOfTwo()).Value = body;

			await command.ExecuteNonQueryAsync();
		}
	}

	private async Task<List<OutboxMessage>> GetOutboxMessages(IDbConnection connection, int maxMessageBatchSize, string correlationId)
	{
		using FbCommand command = connection.CreateCommand();

		if (correlationId != null)
		{
			command.CommandText = $"""
				DELETE FROM {_tableName}
				WHERE "Id" in 
				(
				    select "Id"
				    from {_tableName}
				    where "CorrelationId" = @correlationId
				    order by "Id" asc
				    for update skip locked
				    limit {maxMessageBatchSize}
				)
				returning "Id", "DestinationAddress", "Headers", "Body"
				""";
			command.Parameters.Add("correlationId", FbDbType.VarChar, 16).Value = correlationId;
		}
		else
		{
			command.CommandText = $"""
				DELETE FROM {_tableName}
				WHERE "Id" in 
				(
				    select "Id"
				    from {_tableName}
				    order by "Id" asc
				    for update skip locked
				    limit {maxMessageBatchSize}
				)
				returning "Id", "DestinationAddress", "Headers", "Body"
				""";
		}

		await using global::FirebirdSql.Data.FirebirdClient.FbDataReader reader = await command.ExecuteReaderAsync();

		List<OutboxMessage> messages = new();

		while (await reader.ReadAsync())
		{
			var id = (long)reader["id"];
			var destinationAddress = (string)reader["destinationAddress"];
			Dictionary<string, string> headers = HeaderSerializer.DeserializeFromString((string)reader["headers"]);
			var body = (byte[])reader["body"];
			messages.Add(new OutboxMessage(id, destinationAddress, headers, body));
		}

		return messages;
	}

	private static string SerializeHeaders(Dictionary<string, string> headers) => HeaderSerializer.SerializeToString(headers);
}
