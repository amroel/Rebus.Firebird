using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging;

namespace Rebus.Firebird.Tests.Testcontainers.Firebird;

/// <summary>
/// Initializes a new instance of the <see cref="FirebirdContainer" /> class.
/// </summary>
/// <param name="configuration">The container configuration.</param>
/// <param name="logger">The logger.</param>
public class FirebirdContainer(FirebirdConfiguration configuration, ILogger logger)
	: DockerContainer(configuration, logger)
{
	private readonly FirebirdConfiguration _cfg = configuration;

	/// <summary>
	/// Gets the Firebird connection string.
	/// </summary>
	/// <returns>The Firebird connection string.</returns>
	public string GetConnectionString()
	{
		Dictionary<string, string?> properties = new()
		{
			{ "Server", Hostname },
			{ "Port", $"{GetMappedPublicPort(FirebirdBuilder.FIREBIRD_PORT)}" },
			{ "Database", _cfg.DatabaseName },
			{ "User Id", _cfg.Username },
			{ "Password", _cfg.Password }
		};
		return string.Join(";", properties.Select(property => string.Join("=", property.Key, property.Value)));
	}
}