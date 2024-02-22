using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Rebus.Firebird.Tests.Testcontainers.Firebird;

public sealed class FirebirdBuilder : ContainerBuilder<FirebirdBuilder, FirebirdContainer, FirebirdConfiguration>
{
	private const string FIREBIRD_SYSDBA = "sysdba";

	public const string DEFAULT_FIREBIRD_IMAGE = "jacobalberty/firebird:4.0.1";
	public const int FIREBIRD_PORT = 3050;
	public const string DEFAULT_DATABASE = "test";
	public const string DEFAULT_USERNAME = "test";
	public const string DEFAULT_PASSWORD = "test";

	/// <summary>
	/// Initializes a new instance of the <see cref="FirebirdBuilder" /> class.
	/// </summary>
	public FirebirdBuilder()
		: this(new FirebirdConfiguration()) => DockerResourceConfiguration = Init().DockerResourceConfiguration;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirebirdBuilder" /> class.
	/// </summary>
	/// <param name="configuration">The Docker resource configuration.</param>
	private FirebirdBuilder(FirebirdConfiguration configuration)
		: base(configuration) => DockerResourceConfiguration = configuration;

	/// <inheritdoc />
	protected override FirebirdConfiguration DockerResourceConfiguration { get; }

	/// <summary>
	/// Sets the Firebird database.
	/// </summary>
	/// <param name="databaseName">The Firebird database.</param>
	/// <returns>A configured instance of <see cref="FirebirdBuilder" />.</returns>
	public FirebirdBuilder WithDatabaseName(string databaseName)
		=> Merge(DockerResourceConfiguration, new FirebirdConfiguration(databaseName: databaseName))
			.WithEnvironment("FIREBIRD_DATABASE", databaseName);

	/// <summary>
	/// Sets the Firebird username.
	/// </summary>
	/// <param name="username">The Firebird username.</param>
	/// <returns>A configured instance of <see cref="FirebirdBuilder" />.</returns>
	public FirebirdBuilder WithUsername(string username)
	{
		var newCfg = Merge(DockerResourceConfiguration, new FirebirdConfiguration(username: username));
		return FIREBIRD_SYSDBA.Equals(username, StringComparison.OrdinalIgnoreCase)
			? newCfg
			: newCfg.WithEnvironment("FIREBIRD_USER", username);
	}

	/// <summary>
	/// Sets the Firebird password.
	/// </summary>
	/// <param name="password">The Firebird password.</param>
	/// <returns>A configured instance of <see cref="FirebirdBuilder" />.</returns>
	public FirebirdBuilder WithPassword(string password)
		=> Merge(DockerResourceConfiguration, new FirebirdConfiguration(password: password))
			.WithEnvironment("FIREBIRD_PASSWORD", password)
			.WithEnvironment("ISC_PASSWORD", password);

	public FirebirdBuilder WithTimeZone(string timeZone)
		=> Merge(DockerResourceConfiguration, new FirebirdConfiguration(timeZone: timeZone))
			.WithEnvironment("TZ", timeZone);

	public FirebirdBuilder EnableLegacyClientAuth(bool enable = true)
		=> Merge(DockerResourceConfiguration, new FirebirdConfiguration(enableLegacyAuth: enable))
		.WithEnvironment("EnableLegacyClientAuth", enable ? "true" : "false");

	/// <inheritdoc />
	public override FirebirdContainer Build()
	{
		Validate();
		return new FirebirdContainer(DockerResourceConfiguration, TestcontainersSettings.Logger);
	}

	/// <inheritdoc />
	protected override FirebirdBuilder Init() => base.Init()
		.WithImage(DEFAULT_FIREBIRD_IMAGE)
		.WithPortBinding(FIREBIRD_PORT, true)
		.WithDatabaseName(DEFAULT_DATABASE)
		.WithUsername(DEFAULT_USERNAME)
		.WithPassword(DEFAULT_PASSWORD);


	/// <inheritdoc />
	protected override FirebirdBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
		=> Merge(DockerResourceConfiguration, new FirebirdConfiguration(resourceConfiguration));

	/// <inheritdoc />
	protected override FirebirdBuilder Clone(IContainerConfiguration resourceConfiguration)
		=> Merge(DockerResourceConfiguration, new FirebirdConfiguration(resourceConfiguration));

	/// <inheritdoc />
	protected override FirebirdBuilder Merge(FirebirdConfiguration oldValue, FirebirdConfiguration newValue)
		=> new(new FirebirdConfiguration(oldValue, newValue));
}
