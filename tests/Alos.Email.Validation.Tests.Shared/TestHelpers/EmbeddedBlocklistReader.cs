namespace Alos.Email.Validation.Tests.Shared.TestHelpers;

using Alos.Email.Validation;

/// <summary>
///   Reads the blocklist embedded in the library assembly, so tests state the embedded entries without hardcoding them.
/// </summary>
public static class EmbeddedBlocklistReader
{
  #region Methods - Public

  /// <summary>
  ///   Returns the domains of the embedded primary blocklist: trimmed lines, blank and <c>#</c> lines skipped.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     The line filter is the tests' own copy on purpose: an independent statement of what the embedded list holds,
  ///     so a defect in the library's parser cannot agree with itself.
  ///   </para>
  /// </remarks>
  public static HashSet<string> ReadEmbeddedBlocklistDomains()
  {
    var assembly = typeof(BlocklistFileNames).Assembly;
    var resourceName = assembly
      .GetManifestResourceNames()
      .Single(name => name.EndsWith(BlocklistFileNames.PrimaryBlocklist, StringComparison.Ordinal));

    using var stream = assembly.GetManifestResourceStream(resourceName)!;
    using var reader = new StreamReader(stream);

    var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    while (reader.ReadLine() is { } line)
    {
      var trimmed = line.Trim();

      if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
      {
        domains.Add(trimmed);
      }
    }

    return domains;
  }

  #endregion
}
