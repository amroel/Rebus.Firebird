# Rebus.Firebird
![](https://raw.githubusercontent.com/amroel/Rebus.Firebird/main/artwork/little_rebusbus2_copy-200x200.png)
![](https://raw.githubusercontent.com/amroel/Rebus.Firebird/main/artwork/firebird-logo-200.png)

[![install from nuget](https://img.shields.io/nuget/v/Rebus.Firebird.svg?style=flat-square)](https://www.nuget.org/packages/Rebus.Firebird)

Currently Provides only a FirebirdSQL-based outbox persistence for [Rebus](https://github.com/rebus-org/Rebus) 

## Configuration

```csharp
Configure.With(...)
	.Transport(...)
	...
	.Outbox(o => o.StoreInFirebird(ConnectionString, sender: "sender", tableName: "rebus_oubox"))
```
The sender parameter is used to identify the sender of the message in the outbox. This is useful when multiple senders are using the same outbox table.
The Firebird outbox storage will create an Index on the sender column to speed up the lookup of messages in the outbox.
The Index will be named `IX_{tableName}_sender` where `{tableName}` is the name of the outbox table.
So be sure to:
- use a unique sender name for each sender
- follow the Firebird naming rules for identifiers (before v4.0: 31 characters, since v4.0: 63 characters)

## Usage
Outside of a Rebus handler:
start a database transaction and use the `RebusTransactionScope` with `UseOutbox` to use the outbox.
Example:

```csharp
await using FbConnection connection = new(ConnectionString);
await connection.OpenAsync();
await using FbTransaction transaction = await connection.BeginTransactionAsync();

// this is how we would use the outbox for outgoing messages
using RebusTransactionScope scope = new();
scope.UseOutbox(connection, transaction);

await bus.Send(new SomeMessage());

await scope.CompleteAsync();
await transaction.CommitAsync();
```

Inside a Rebus handler:
nothing special to do, the outbox will be used automatically

## Reporting
You can pass an Implementation of `IReportOutboxOperations` to the Outbox configuration to get reports about the outbox operations.
The Interface `IReportOutboxOperations` has 6 methods:
- void ReportChecking(); // called when the outbox is checked for pending messages
- void ReportNoPendingMessages(); // called when no pending messages are found in the outbox
- void ReportSending(int count); // called when messages are sent from the outbox
- void ReportRetrying(int attempt); // called when a sending a message is retried
- void ReportSendingFailed(int count); // called when sending messages from the outbox failed
- void ReportSent(int count); // called when messages are successfully sent from the outbox



---


