using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Rebus.Firebird.Tests.Testcontainers.Firebird;

public class FirebirdConfiguration : ContainerConfiguration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FirebirdConfiguration" /> class.
	/// </summary>
	/// <param name="databaseName">The Firebird database name.</param>
	/// <param name="username">The Firebird username.</param>
	/// <param name="password">The Firebird password.</param>
	public FirebirdConfiguration(string? databaseName = default,
		string? username = default,
		string? password = default,
		bool? enableLegacyAuth = default,
		string? timeZone = default)
	{
		DatabaseName = databaseName;
		Username = username;
		Password = password;
		EnableLegacyAuth = enableLegacyAuth;
		TimeZone = timeZone;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirebirdConfiguration" /> class.
	/// </summary>
	/// <param name="resourceConfiguration">The Docker resource configuration.</param>
	public FirebirdConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
		: base(resourceConfiguration)
	{
		// Passes the configuration upwards to the base implementations to create an updated immutable copy.
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirebirdConfiguration" /> class.
	/// </summary>
	/// <param name="resourceConfiguration">The Docker resource configuration.</param>
	public FirebirdConfiguration(IContainerConfiguration resourceConfiguration)
		: base(resourceConfiguration)
	{
		// Passes the configuration upwards to the base implementations to create an updated immutable copy.
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirebirdConfiguration" /> class.
	/// </summary>
	/// <param name="resourceConfiguration">The Docker resource configuration.</param>
	public FirebirdConfiguration(FirebirdConfiguration resourceConfiguration)
		: this(new FirebirdConfiguration(), resourceConfiguration)
	{
		// Passes the configuration upwards to the base implementations to create an updated immutable copy.
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirebirdConfiguration" /> class.
	/// </summary>
	/// <param name="oldValue">The old Docker resource configuration.</param>
	/// <param name="newValue">The new Docker resource configuration.</param>
	public FirebirdConfiguration(FirebirdConfiguration oldValue, FirebirdConfiguration newValue)
		: base(oldValue, newValue)
	{
		DatabaseName = BuildConfiguration.Combine(oldValue.DatabaseName, newValue.DatabaseName);
		Username = BuildConfiguration.Combine(oldValue.Username, newValue.Username);
		Password = BuildConfiguration.Combine(oldValue.Password, newValue.Password);
		EnableLegacyAuth = BuildConfiguration.Combine(oldValue.EnableLegacyAuth, newValue.EnableLegacyAuth);
		TimeZone = BuildConfiguration.Combine(oldValue.TimeZone, newValue.TimeZone);
	}

	public string? DatabaseName { get; }
	public string? Username { get; }
	public string? Password { get; }
	public bool? EnableLegacyAuth { get; }
	public string? TimeZone { get; }
}