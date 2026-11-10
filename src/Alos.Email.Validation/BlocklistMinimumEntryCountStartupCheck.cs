namespace Alos.Email.Validation;

using Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
///   Aborts the host start when <see cref="EmailValidationOptions.MinimumBlocklistEntryCount"/> is above 0 and the
///   checker's loaded blocked set holds fewer entries.
/// </summary>
/// <remarks>
///   <para>
///     A <c>ValidateOnStart</c> validator cannot do this: the checker takes <see cref="IOptions{TOptions}"/> of
///     <see cref="EmailValidationOptions"/>, whose options factory would take that validator, a circular resolution.
///   </para>
/// </remarks>
internal sealed class BlocklistMinimumEntryCountStartupCheck(
  IOptions<EmailValidationOptions> options,
  IServiceProvider                 serviceProvider) : IHostedLifecycleService
{
  #region Methods - Public

  /// <inheritdoc />
  /// <exception cref="OptionsValidationException">Thrown when the loaded blocked set holds fewer entries than the floor.</exception>
  public Task StartingAsync(CancellationToken cancellationToken)
  {
    var minimumEntryCount = options.Value.MinimumBlocklistEntryCount;

    if (minimumEntryCount <= 0)
      return Task.CompletedTask;

    // Resolved here, never injected, so a host with no floor does not construct the checker at start.
    var loadedBlockedDomainCount = serviceProvider.GetRequiredService<IDisposableEmailDomainChecker>().BlockedDomainCount;

    if (loadedBlockedDomainCount >= minimumEntryCount)
      return Task.CompletedTask;

    var failure =
      $"{EmailValidationOptions.SectionName}:{nameof(EmailValidationOptions.MinimumBlocklistEntryCount)} is {minimumEntryCount}, " +
      $"but the loaded disposable domain blocklist holds {loadedBlockedDomainCount} entries.";

    throw new OptionsValidationException(Options.DefaultName, typeof(EmailValidationOptions), [failure]);
  }


  /// <inheritdoc />
  public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;


  /// <inheritdoc />
  public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;


  /// <inheritdoc />
  public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;


  /// <inheritdoc />
  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;


  /// <inheritdoc />
  public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  #endregion
}
