namespace Alos.Email.Validation.IntegrationTests;

using Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tests.Shared.Fixtures;

/// <summary>
///   Integration tests for <see cref="ServiceCollectionExtensions"/> DI registration.
///   Tests that services are correctly registered and can be resolved from the container.
/// </summary>
public sealed class ServiceCollectionExtensionsIntegrationTests : IDisposable
{
  #region Fields

  private readonly TempFileFixture _fixture;

  #endregion


  #region Constructors

  public ServiceCollectionExtensionsIntegrationTests()
  {
    _fixture = new TempFileFixture();
  }

  #endregion


  #region Tests - AddEmailValidation with IConfiguration

  [Fact]
  public void AddEmailValidation_WithConfiguration_RegistersServices()
  {
    // Arrange: Create configuration with EmailValidation section.
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["EmailValidation:EnableAutoUpdate"] = "false",
        ["EmailValidation:UpdateInterval"] = "12:00:00"
      })
      .Build();

    var services = new ServiceCollection();
    services.AddLogging();

    // Act: Register services using configuration.
    services.AddEmailValidation(configuration);
    using var provider = services.BuildServiceProvider();

    // Assert: All core services should be resolvable.
    provider.GetService<IDisposableEmailDomainChecker>().Should().NotBeNull();
    provider.GetService<IMxRecordValidator>().Should().NotBeNull();
    provider.GetService<IEmailValidationService>().Should().NotBeNull();
  }

  #endregion


  #region Tests - AddEmailValidation with Action

  [Fact]
  public void AddEmailValidation_WithAction_RegistersServices()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();

    // Act: Register services using action configuration.
    services.AddEmailValidation(options =>
    {
      options.EnableAutoUpdate = false;
      options.UpdateInterval = TimeSpan.FromHours(6);
    });
    using var provider = services.BuildServiceProvider();

    // Assert: All core services should be resolvable.
    provider.GetService<IDisposableEmailDomainChecker>().Should().NotBeNull();
    provider.GetService<IMxRecordValidator>().Should().NotBeNull();
    provider.GetService<IEmailValidationService>().Should().NotBeNull();
  }

  #endregion


  #region Tests - Service Resolution

  [Fact]
  public void AddEmailValidation_ResolvesIEmailValidationService()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    // Act
    using var provider = services.BuildServiceProvider();
    var service = provider.GetRequiredService<IEmailValidationService>();

    // Assert
    service.Should().NotBeNull();
    service.Should().BeOfType<EmailValidationService>();
  }


  [Fact]
  public void AddEmailValidation_ResolvesIDisposableEmailDomainChecker()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    // Act
    using var provider = services.BuildServiceProvider();
    var checker = provider.GetRequiredService<IDisposableEmailDomainChecker>();

    // Assert
    checker.Should().NotBeNull();
    checker.Should().BeOfType<DisposableEmailDomainChecker>();
  }


  [Fact]
  public void AddEmailValidation_ResolvesIMxRecordValidator()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    // Act
    using var provider = services.BuildServiceProvider();
    var validator = provider.GetRequiredService<IMxRecordValidator>();

    // Assert
    validator.Should().NotBeNull();
    validator.Should().BeOfType<MxRecordValidator>();
  }

  #endregion


  #region Tests - Service Lifetime

  [Fact]
  public void AddEmailValidation_SingletonLifetime()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();
    using var provider = services.BuildServiceProvider();

    // Act: Resolve services twice.
    var checker1 = provider.GetRequiredService<IDisposableEmailDomainChecker>();
    var checker2 = provider.GetRequiredService<IDisposableEmailDomainChecker>();

    var validator1 = provider.GetRequiredService<IMxRecordValidator>();
    var validator2 = provider.GetRequiredService<IMxRecordValidator>();

    var service1 = provider.GetRequiredService<IEmailValidationService>();
    var service2 = provider.GetRequiredService<IEmailValidationService>();

    // Assert: Should be same instances (singleton).
    checker1.Should().BeSameAs(checker2);
    validator1.Should().BeSameAs(validator2);
    service1.Should().BeSameAs(service2);
  }

  #endregion


  #region Tests - ServiceProvider Disposal Chain

  [Fact]
  public void ServiceProvider_Dispose_DisposesDisposableEmailDomainChecker()
  {
    // Arrange: Create and dispose a ServiceProvider.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    var provider = services.BuildServiceProvider();
    var checker = provider.GetRequiredService<IDisposableEmailDomainChecker>();

    // Verify the checker works before disposal.
    checker.IsDisposable("mailinator.com").Should().BeTrue();

    // Act: Dispose the ServiceProvider.
    provider.Dispose();

    // Assert: The checker should now throw ObjectDisposedException.
    // This verifies the DI container disposes the singleton when it's disposed.
    FluentActions
      .Invoking(() => checker.IsDisposable("test.com"))
      .Should().Throw<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }


  [Fact]
  public async Task ServiceProvider_DisposeAsync_DisposesDisposableEmailDomainChecker()
  {
    // Arrange: Create and dispose a ServiceProvider asynchronously.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation();

    var provider = services.BuildServiceProvider();
    var checker = provider.GetRequiredService<IDisposableEmailDomainChecker>();

    // Verify the checker works before disposal.
    checker.IsDisposable("mailinator.com").Should().BeTrue();

    // Act: Dispose the ServiceProvider asynchronously.
    await provider.DisposeAsync();

    // Assert: The checker should now throw ObjectDisposedException.
    FluentActions
      .Invoking(() => checker.IsDisposable("test.com"))
      .Should().Throw<ObjectDisposedException>()
      .WithMessage("*DisposableEmailDomainChecker*");
  }

  #endregion


  #region Tests - AddEmailValidationWithAutoUpdate

  [Fact]
  public void AddEmailValidationWithAutoUpdate_RegistersHostedService()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();

    // Act
    services.AddEmailValidationWithAutoUpdate(options =>
    {
      options.BlocklistDirectory = _fixture.TempDirectory;
      options.EnableAutoUpdate = true;
    });
    using var provider = services.BuildServiceProvider();

    // Assert: BlocklistUpdater should be registered as a hosted service.
    var hostedServices = provider.GetServices<IHostedService>();
    hostedServices.Should().ContainSingle(s => s is BlocklistUpdater);
  }


  [Fact]
  public void AddEmailValidationWithAutoUpdate_RegistersHttpClient()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();

    // Act
    services.AddEmailValidationWithAutoUpdate(options =>
    {
      options.BlocklistDirectory = _fixture.TempDirectory;
      options.EnableAutoUpdate = true;
    });
    using var provider = services.BuildServiceProvider();

    // Assert: IHttpClientFactory should be available.
    var httpClientFactory = provider.GetService<IHttpClientFactory>();
    httpClientFactory.Should().NotBeNull();
  }

  #endregion


  #region Tests - Configuration Binding

  [Fact]
  public void Configuration_BindsAllOptions()
  {
    // Arrange: Create configuration with all EmailValidation options.
    var blocklistDir = _fixture.TempDirectory;
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["EmailValidation:BlocklistDirectory"] = blocklistDir,
        ["EmailValidation:EnableAutoUpdate"] = "true",
        ["EmailValidation:UpdateInterval"] = "06:00:00",
        ["EmailValidation:BlocklistUrl"] = "https://example.com/blocklist.conf",
        ["EmailValidation:AllowlistUrl"] = "https://example.com/allowlist.conf"
      })
      .Build();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation(configuration);
    using var provider = services.BuildServiceProvider();

    // Act: Resolve options.
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailValidationOptions>>();

    // Assert: All properties should be bound.
    options.Value.BlocklistDirectory.Should().Be(blocklistDir);
    options.Value.EnableAutoUpdate.Should().BeTrue();
    options.Value.UpdateInterval.Should().Be(TimeSpan.FromHours(6));
    options.Value.BlocklistUrl.Should().Be("https://example.com/blocklist.conf");
    options.Value.AllowlistUrl.Should().Be("https://example.com/allowlist.conf");
  }


  [Fact]
  public void Configuration_DefaultValues_Applied()
  {
    // Arrange: Create configuration with minimal/empty section.
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["EmailValidation:EnableAutoUpdate"] = "false" // Only set one property.
      })
      .Build();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation(configuration);
    using var provider = services.BuildServiceProvider();

    // Act: Resolve options.
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailValidationOptions>>();

    // Assert: Default values should be applied.
    options.Value.BlocklistDirectory.Should().BeNull();
    options.Value.EnableAutoUpdate.Should().BeFalse();
    options.Value.UpdateInterval.Should().Be(TimeSpan.FromHours(24)); // Default.
    options.Value.BlocklistUrl.Should().Contain("disposable-email-domains");
    options.Value.AllowlistUrl.Should().Contain("disposable-email-domains");
    options.Value.CustomBlocklist.Should().BeEmpty();
    options.Value.CustomAllowlist.Should().BeEmpty();
  }


  [Fact]
  public void Configuration_CustomLists_BoundAsLists()
  {
    // Arrange: Create configuration with custom list arrays.
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["EmailValidation:CustomBlocklist:0"] = "custom-blocked-1.com",
        ["EmailValidation:CustomBlocklist:1"] = "custom-blocked-2.com",
        ["EmailValidation:CustomAllowlist:0"] = "custom-allowed-1.com"
      })
      .Build();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddEmailValidation(configuration);
    using var provider = services.BuildServiceProvider();

    // Act: Resolve options.
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailValidationOptions>>();

    // Assert: Lists should be populated.
    options.Value.CustomBlocklist.Should().HaveCount(2);
    options.Value.CustomBlocklist.Should().Contain("custom-blocked-1.com");
    options.Value.CustomBlocklist.Should().Contain("custom-blocked-2.com");
    options.Value.CustomAllowlist.Should().HaveCount(1);
    options.Value.CustomAllowlist.Should().Contain("custom-allowed-1.com");
  }

  #endregion


  #region IDisposable

  public void Dispose()
  {
    _fixture.Dispose();
  }

  #endregion
}
