namespace Alos.Email.Validation.Tests.Shared.Fixtures;

using Alos.Email.Validation;

/// <summary>
///   Provides temporary file and directory management for integration tests.
///   Automatically cleans up created files and directories when disposed.
/// </summary>
/// <remarks>
///   <para>
///     Each test can create isolated temporary directories and blocklist files
///     without worrying about cleanup. All created paths are tracked and removed
///     when the fixture is disposed.
///   </para>
/// </remarks>
public sealed class TempFileFixture : IDisposable
{
  #region Fields

  private readonly List<string> _createdPaths = [];
  private bool _disposed;

  #endregion


  #region Properties

  /// <summary>
  ///   Gets the root temporary directory for this fixture instance.
  /// </summary>
  public string TempDirectory { get; }

  #endregion


  #region Constructors

  /// <summary>
  ///   Initializes a new instance of the <see cref="TempFileFixture"/> class.
  ///   Creates a unique temporary directory for this test run.
  /// </summary>
  public TempFileFixture()
  {
    TempDirectory = Path.Combine(
      Path.GetTempPath(),
      "Alos.Email.Validation.Tests",
      Guid.NewGuid().ToString("N"));

    Directory.CreateDirectory(TempDirectory);
    _createdPaths.Add(TempDirectory);
  }

  #endregion


  #region Methods - Public

  /// <summary>
  ///   Creates a blocklist file with the specified domains.
  /// </summary>
  /// <param name="domains">The domains to include in the blocklist.</param>
  /// <returns>The full path to the created blocklist file.</returns>
  public string CreateBlocklistFile(params string[] domains)
  {
    return CreateBlocklistFile(TempDirectory, domains);
  }


  /// <summary>
  ///   Creates a blocklist file with the specified domains in a specific directory.
  /// </summary>
  /// <param name="directory">The directory to create the file in.</param>
  /// <param name="domains">The domains to include in the blocklist.</param>
  /// <returns>The full path to the created blocklist file.</returns>
  public string CreateBlocklistFile(string directory, params string[] domains)
  {
    EnsureDirectoryExists(directory);

    var path = Path.Combine(directory, BlocklistFileNames.PrimaryBlocklist);

    File.WriteAllLines(path, domains);
    _createdPaths.Add(path);

    return path;
  }


  /// <summary>
  ///   Creates an allowlist file with the specified domains.
  /// </summary>
  /// <param name="domains">The domains to include in the allowlist.</param>
  /// <returns>The full path to the created allowlist file.</returns>
  public string CreateAllowlistFile(params string[] domains)
  {
    return CreateAllowlistFile(TempDirectory, domains);
  }


  /// <summary>
  ///   Creates an allowlist file with the specified domains in a specific directory.
  /// </summary>
  /// <param name="directory">The directory to create the file in.</param>
  /// <param name="domains">The domains to include in the allowlist.</param>
  /// <returns>The full path to the created allowlist file.</returns>
  public string CreateAllowlistFile(string directory, params string[] domains)
  {
    EnsureDirectoryExists(directory);

    var path = Path.Combine(directory, BlocklistFileNames.PrimaryAllowlist);

    File.WriteAllLines(path, domains);
    _createdPaths.Add(path);

    return path;
  }


  /// <summary>
  ///   Creates a custom list file with the specified domains at a specific path.
  /// </summary>
  /// <param name="fileName">The file name (will be created in TempDirectory).</param>
  /// <param name="domains">The domains to include in the file.</param>
  /// <returns>The full path to the created file.</returns>
  public string CreateCustomListFile(string fileName, params string[] domains)
  {
    var path = Path.Combine(TempDirectory, fileName);

    File.WriteAllLines(path, domains);
    _createdPaths.Add(path);

    return path;
  }


  /// <summary>
  ///   Creates a custom list file with comments and the specified domains.
  /// </summary>
  /// <param name="fileName">The file name (will be created in TempDirectory).</param>
  /// <param name="lines">The lines to write (can include comments starting with #).</param>
  /// <returns>The full path to the created file.</returns>
  public string CreateCustomListFileWithComments(string fileName, params string[] lines)
  {
    var path = Path.Combine(TempDirectory, fileName);

    File.WriteAllLines(path, lines);
    _createdPaths.Add(path);

    return path;
  }


  /// <summary>
  ///   Creates an empty file at the specified path.
  /// </summary>
  /// <param name="fileName">The file name (will be created in TempDirectory).</param>
  /// <returns>The full path to the created file.</returns>
  public string CreateEmptyFile(string fileName)
  {
    var path = Path.Combine(TempDirectory, fileName);

    File.WriteAllText(path, string.Empty);
    _createdPaths.Add(path);

    return path;
  }


  /// <summary>
  ///   Creates a subdirectory within the temporary directory.
  /// </summary>
  /// <param name="subdirectoryName">The name of the subdirectory to create.</param>
  /// <returns>The full path to the created subdirectory.</returns>
  public string CreateSubdirectory(string subdirectoryName)
  {
    var path = Path.Combine(TempDirectory, subdirectoryName);

    Directory.CreateDirectory(path);
    _createdPaths.Add(path);

    return path;
  }


  /// <summary>
  ///   Gets the path where a file would be located (does not create it).
  /// </summary>
  /// <param name="fileName">The file name.</param>
  /// <returns>The full path where the file would be located.</returns>
  public string GetFilePath(string fileName)
  {
    return Path.Combine(TempDirectory, fileName);
  }

  #endregion


  #region Methods - Private

  /// <summary>
  ///   Ensures the specified directory exists.
  /// </summary>
  private void EnsureDirectoryExists(string directory)
  {
    if (!Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);

      if (!_createdPaths.Contains(directory))
        _createdPaths.Add(directory);
    }
  }

  #endregion


  #region IDisposable

  /// <inheritdoc />
  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;

    // Clean up in reverse order (files before directories).
    foreach (var path in _createdPaths.AsEnumerable().Reverse())
    {
      try
      {
        if (File.Exists(path))
          File.Delete(path);
        else if (Directory.Exists(path))
          Directory.Delete(path, recursive: true);
      }
      catch
      {
        // Best effort cleanup - ignore errors.
      }
    }
  }

  #endregion
}
