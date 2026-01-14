namespace Alos.Email.Validation;

using Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///   Provides extension methods for setting up email validation services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
  #region Methods - Public

  /// <summary>
  ///   Adds email validation services using a configuration section.
  /// </summary>
  /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
  /// <example>
  ///   <code>
  ///   // In Program.cs
  ///   builder.Services.AddEmailValidation(builder.Configuration);
  ///   </code>
  /// </example>
  public static IServiceCollection AddEmailValidation(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    return services.AddEmailValidation(options =>
      configuration.GetSection(EmailValidationOptions.SectionName).Bind(options));
  }


  /// <summary>
  ///   Adds email validation services with programmatic configuration.
  /// </summary>
  /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
  /// <param name="configureOptions">An action to configure the <see cref="EmailValidationOptions" />.</param>
  /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
  /// <example>
  ///   <code>
  ///   // In Program.cs
  ///   builder.Services.AddEmailValidation(options =>
  ///   {
  ///       options.BlocklistDirectory = "/var/lib/alos/email-blocklists";
  ///   });
  ///   </code>
  /// </example>
  public static IServiceCollection AddEmailValidation(
    this IServiceCollection services,
    Action<EmailValidationOptions>? configureOptions = null)
  {
    if (configureOptions is not null)
      services.Configure(configureOptions);

    // Register core services as singletons (thread-safe, expensive to create)
    services.AddSingleton<IDisposableEmailDomainChecker, DisposableEmailDomainChecker>();
    services.AddSingleton<IMxRecordValidator, MxRecordValidator>();
    services.AddSingleton<IEmailValidationService, EmailValidationService>();

    return services;
  }


  /// <summary>
  ///   Adds email validation services with background auto-update of blocklists.
  /// </summary>
  /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
  /// <param name="configuration">The application configuration.</param>
  /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
  /// <remarks>
  ///   <para>
  ///     This overload registers a <see cref="BlocklistUpdater"/> background service that
  ///     periodically downloads the latest blocklists from GitHub.
  ///   </para>
  ///   <para>
  ///     Requires <see cref="EmailValidationOptions.BlocklistDirectory"/> to be configured
  ///     and <see cref="EmailValidationOptions.EnableAutoUpdate"/> to be true.
  ///   </para>
  /// </remarks>
  /// <example>
  ///   <code>
  ///   // In Program.cs
  ///   builder.Services.AddEmailValidationWithAutoUpdate(builder.Configuration);
  ///   </code>
  /// </example>
  public static IServiceCollection AddEmailValidationWithAutoUpdate(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    return services.AddEmailValidationWithAutoUpdate(options =>
      configuration.GetSection(EmailValidationOptions.SectionName).Bind(options));
  }


  /// <summary>
  ///   Adds email validation services with background auto-update of blocklists.
  /// </summary>
  /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
  /// <param name="configureOptions">An action to configure the <see cref="EmailValidationOptions" />.</param>
  /// <returns>The <see cref="IServiceCollection" /> so that additional calls can be chained.</returns>
  /// <example>
  ///   <code>
  ///   // In Program.cs
  ///   builder.Services.AddEmailValidationWithAutoUpdate(options =>
  ///   {
  ///       options.BlocklistDirectory = "/var/lib/alos/email-blocklists";
  ///       options.EnableAutoUpdate = true;
  ///       options.UpdateInterval = TimeSpan.FromHours(12);
  ///   });
  ///   </code>
  /// </example>
  public static IServiceCollection AddEmailValidationWithAutoUpdate(
    this IServiceCollection services,
    Action<EmailValidationOptions> configureOptions)
  {
    services.AddEmailValidation(configureOptions);
    services.AddHttpClient();
    services.AddHostedService<BlocklistUpdater>();

    return services;
  }

  #endregion
}
