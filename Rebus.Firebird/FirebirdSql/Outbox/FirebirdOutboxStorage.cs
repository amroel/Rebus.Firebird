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
public class FirebirdOutboxStorage(Func<ITransactionContext, IDbConnection> connectionProvider, TableName tableName)
	: IOutboxStorage, IInitializable
{
	private static readonly HeaderSerializer _headerSerializer = new();
	private readonly Func<ITransactionContext, IDbConnection> _connectionProvider = connectionProvider
		?? throw new ArgumentNullException(nameof(connectionProvider));
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

			if (connection.TableExists(_tableName))
				return;

			try
			{
				using FbCommand command = connection.CreateCommand();

				command.CommandText = $"""
					create table {_tableName}
					(
					    id bigint not null primary key,
					    correlation_id varchar(16),
					    message_id varchar(255),
					    source_queue varchar(255),
					    destination_address varchar(255) not null,
					    headers blob sub_type text,
					    body blob sub_type binary,
					    sent boolean default false not null
					)
					""";

				await command.ExecuteNonQueryAsync();

				command.CommandText = $"create or alter sequence {_tableName}_seq restart";
				await command.ExecuteNonQueryAsync();

				await connection.Complete();
			}
			catch (Exception)
			{
				if (!connection.TableExists(_tableName))
				{
					throw;
				}
			}

			await scope.CompleteAsync();
		}

		AsyncHelper.RunSync(InitializeAsync);
	}

	/// <summary>
	/// Stores the given <paramref name="outgoingMessages"/> as being the result of processing message with 
	/// ID <paramref name="messageId"/>
	/// in the queue of this particular endpoint. 
	/// If <paramref name="outgoingMessages"/> is an empty sequence, a note is made of the fact
	/// that the message with ID <paramref name="messageId"/> has been processed.
	/// </summary>
	public async Task Save(IEnumerable<OutgoingTransportMessage> outgoingMessages,
		string? messageId = null,
		string? sourceQueue = null,
		string? correlationId = null) => await InnerSave(outgoingMessages, messageId, sourceQueue, correlationId);

	/// <summary>
	/// Stores the given <paramref name="outgoingMessages"/> using the given <paramref name="dbConnection"/>.
	/// </summary>
	public async Task Save(IEnumerable<OutgoingTransportMessage> outgoingMessages, IDbConnection dbConnection)
		=> await SaveUsingConnection(dbConnection, outgoingMessages);

	/// <inheritdoc />
	public async Task<OutboxMessageBatch> GetNextMessageBatch(string? correlationId = null,
		int maxMessageBatchSize = 100) => await InnerGetMessageBatch(maxMessageBatchSize, correlationId);

	private async Task<OutboxMessageBatch> InnerGetMessageBatch(int maxMessageBatchSize, string? correlationId)
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

			// this must be done when cleaning up
			void Dispose()
			{
				connection.Dispose();
				scope.Dispose();
			}

			try
			{
				List<OutboxMessage> messages = await GetOutboxMessages(connection, maxMessageBatchSize, correlationId);

				// bail out if no messages were found
				if (messages.Count == 0)
					return OutboxMessageBatch.Empty(Dispose);

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

	private async Task InnerSave(IEnumerable<OutgoingTransportMessage> outgoingMessages,
		string? messageId,
		string? sourceQueue,
		string? correlationId)
	{
		using RebusTransactionScope scope = new();
		using IDbConnection connection = _connectionProvider(scope.TransactionContext);

		await SaveUsingConnection(connection, outgoingMessages, messageId, sourceQueue, correlationId);

		await connection.Complete();
		await scope.CompleteAsync();
	}

	private async Task SaveUsingConnection(IDbConnection connection,
		IEnumerable<OutgoingTransportMessage> outgoingMessages,
		string? messageId = null,
		string? sourceQueue = null,
		string? correlationId = null)
	{
		using FbCommand command = connection.CreateCommand();
		command.CommandText = $"""
				insert into {_tableName} 
				(
					id, 
					correlation_id, 
					message_id, 
					source_queue, 
					destination_address, 
					headers, 
					body
				)
				values 
				(
					next value for {_tableName}_seq, 
					@correlationId, 
					@messageId, 
					@sourceQueue, 
					@destinationAddress, 
					@headers, 
					@body
				)
				""";
		command.Parameters.AddWithValue("correlationId", correlationId);
		command.Parameters.AddWithValue("messageId", messageId);
		command.Parameters.AddWithValue("sourceQueue", sourceQueue);
		FbParameter adrParam = command.Parameters.Add("destinationAddress", FbDbType.Text);
		FbParameter hdrParam = command.Parameters.Add("headers", FbDbType.Text);
		FbParameter bdyParam = command.Parameters.Add("body", FbDbType.Binary);

		foreach (OutgoingTransportMessage message in outgoingMessages)
		{

			Messages.TransportMessage transportMessage = message.TransportMessage;
			var body = message.TransportMessage.Body;
			var headers = SerializeHeaders(transportMessage.Headers);

			adrParam.Value = message.DestinationAddress;
			hdrParam.Value = headers;
			bdyParam.Value = body;

			await command.ExecuteNonQueryAsync();
		}
	}

	private async Task<List<OutboxMessage>> GetOutboxMessages(IDbConnection connection,
		int maxMessageBatchSize,
		string? correlationId)
	{
		using FbCommand command = connection.CreateCommand();
		var select = $"""
			select
				id, 
				destination_address, 
				headers, 
				body
			from {_tableName}
			""";
		const string orderByForUpdate = """
			order by 
				id asc
			fetch next @maxBatchSize rows only
			for update
			with lock
			""";
		if (correlationId is not null)
		{
			command.CommandText = $"""
				{select}
				where 
					correlation_id = @correlationId
				{orderByForUpdate}
				""";
			command.Parameters.AddWithValue("correlationId", correlationId);
		}
		else
		{
			command.CommandText = $"""
				{select}
				{orderByForUpdate}
				""";
		}
		command.Parameters.AddWithValue("maxBatchSize", maxMessageBatchSize);

		using FbDataReader reader = await command.ExecuteReaderAsync();

		List<OutboxMessage> messages = [];

		while (await reader.ReadAsync())
		{
			var id = (long)reader["id"];
			var destinationAddress = (string)reader["destination_address"];
			Dictionary<string, string> headers = _headerSerializer.DeserializeFromString((string)reader["headers"]);
			var body = (byte[])reader["body"];
			messages.Add(new OutboxMessage(id, destinationAddress, headers, body));
		}

		if (messages.Count > 0)
		{
			var idParams = string.Join(", ", messages.Select((m, index) => $"@id{index}"));
			command.CommandText = $"delete from {_tableName} where id in ({idParams})";
			command.Parameters.Clear();
			for (var i = 0; i < messages.Count; i++)
			{
				command.Parameters.AddWithValue($"id{i}", messages[i].Id);
			}
			var deleted = await command.ExecuteNonQueryAsync();
		}

		return messages;
	}

	private static string SerializeHeaders(Dictionary<string, string> headers)
		=> _headerSerializer.SerializeToString(headers);
}
