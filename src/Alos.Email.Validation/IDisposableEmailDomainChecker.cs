namespace Alos.Email.Validation;

/// <summary>
///   Checks if an email domain is a known disposable/temporary email provider.
/// </summary>
/// <remarks>
///   <para>
///     Implementations of this interface may implement <see cref="IDisposable"/>. If so,
///     calling <see cref="IsDisposable"/> or <see cref="ReloadFromDisk"/> after disposal
///     will throw <see cref="ObjectDisposedException"/>.
///   </para>
/// </remarks>
public interface IDisposableEmailDomainChecker
{
  /// <summary>
  ///   Checks if a domain is a known disposable email provider.
  /// </summary>
  /// <param name="domain">The email domain (e.g., "mailinator.com").</param>
  /// <returns>
  ///   <c>true</c> if the domain is disposable and should be blocked;
  ///   <c>false</c> if the domain is legitimate or <paramref name="domain"/> is null/empty.
  /// </returns>
  /// <exception cref="ObjectDisposedException">
  ///   Thrown if the checker has been disposed (for implementations that support disposal).
  /// </exception>
  bool IsDisposable(string? domain);

  /// <summary>
  ///   Reloads blocklists from disk. Called by BlocklistUpdater after download.
  /// </summary>
  /// <param name="directory">The directory containing the blocklist files.</param>
  /// <exception cref="ObjectDisposedException">
  ///   Thrown if the checker has been disposed (for implementations that support disposal).
  /// </exception>
  /// <exception cref="DirectoryNotFoundException">
  ///   Thrown if the specified directory does not exist.
  /// </exception>
  void ReloadFromDisk(string directory);
}
