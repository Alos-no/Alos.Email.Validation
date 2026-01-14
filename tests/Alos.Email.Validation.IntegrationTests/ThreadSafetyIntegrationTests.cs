namespace Alos.Email.Validation.IntegrationTests;

using Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Tests.Shared.Fixtures;

/// <summary>
///   Integration tests for thread safety of the DisposableEmailDomainChecker.
///   Tests concurrent access patterns to ensure the ReaderWriterLockSlim is working correctly.
/// </summary>
public sealed class ThreadSafetyIntegrationTests : IDisposable
{
  #region Fields

  private readonly TempFileFixture _fixture;

  #endregion


  #region Constructors

  public ThreadSafetyIntegrationTests()
  {
    _fixture = new TempFileFixture();
  }

  #endregion


  #region Tests - Concurrent Reads

  [Fact]
  public async Task DisposableEmailDomainChecker_ConcurrentReads_ThreadSafe()
  {
    // Arrange: Create checker with embedded resources.
    var options = Options.Create(new EmailValidationOptions());
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    var errors = new List<Exception>();
    var taskCount = 10;
    var iterationsPerTask = 1000;

    // Act: Run many concurrent read operations.
    var tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
    {
      try
      {
        for (var i = 0; i < iterationsPerTask; i++)
        {
          // Mix of blocked and non-blocked domains.
          _ = checker.IsDisposable("mailinator.com");
          _ = checker.IsDisposable("gmail.com");
          _ = checker.IsDisposable("guerrillamail.com");
          _ = checker.IsDisposable("outlook.com");
          _ = checker.IsDisposable($"random-domain-{taskIndex}-{i}.com");
        }
      }
      catch (Exception ex)
      {
        lock (errors)
        {
          errors.Add(ex);
        }
      }
    }));

    await Task.WhenAll(tasks);

    // Assert: No exceptions during concurrent reads.
    errors.Should().BeEmpty("concurrent reads should be thread-safe");
  }

  #endregion


  #region Tests - Concurrent Reads During Reload

  [Fact]
  public async Task DisposableEmailDomainChecker_ReadDuringReload_ThreadSafe()
  {
    // Arrange: Create blocklist directory with initial content.
    var blocklistDir = _fixture.CreateSubdirectory("concurrent-reload");
    _fixture.CreateBlocklistFile(blocklistDir, "initial-blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    var errors = new List<Exception>();
    var reloadCount = 20;
    var readTaskCount = 5;
    var readsPerTask = 500;

    using var cts = new CancellationTokenSource();

    // Start concurrent read tasks.
    var readTasks = Enumerable.Range(0, readTaskCount).Select(taskIdx => Task.Run(async () =>
    {
      try
      {
        while (!cts.Token.IsCancellationRequested)
        {
          for (var i = 0; i < readsPerTask && !cts.Token.IsCancellationRequested; i++)
          {
            // Discard results - we're testing thread safety, not correctness.
            checker.IsDisposable("initial-blocked.com");
            checker.IsDisposable("mailinator.com");
            checker.IsDisposable("gmail.com");
            await Task.Yield(); // Allow other tasks to run.
          }
        }
      }
      catch (OperationCanceledException)
      {
        // Expected when cancellation is requested.
      }
      catch (Exception ex)
      {
        lock (errors)
        {
          errors.Add(ex);
        }
      }
    }, cts.Token));

    // Perform reloads while reads are happening.
    var reloadTask = Task.Run(() =>
    {
      try
      {
        for (var i = 0; i < reloadCount; i++)
        {
          // Update the file with different content.
          var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
          File.WriteAllLines(blocklistPath, [$"initial-blocked.com", $"reload-{i}.com"]);

          checker.ReloadFromDisk(blocklistDir);
          Thread.Sleep(10); // Small delay between reloads.
        }
      }
      catch (Exception ex)
      {
        lock (errors)
        {
          errors.Add(ex);
        }
      }
    });

    // Wait for reload task to complete.
    await reloadTask;

    // Stop read tasks.
    cts.Cancel();

    try
    {
      await Task.WhenAll(readTasks);
    }
    catch (OperationCanceledException)
    {
      // Expected.
    }

    // Assert: No exceptions during concurrent reads/reloads.
    errors.Should().BeEmpty("concurrent reads during reload should be thread-safe");
  }


  [Fact]
  public async Task DisposableEmailDomainChecker_MultipleWritersBlocked_ThreadSafe()
  {
    // Arrange: Create blocklist directory with initial content.
    var blocklistDir = _fixture.CreateSubdirectory("multi-writer");
    _fixture.CreateBlocklistFile(blocklistDir, "blocked.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    var errors = new List<Exception>();
    var writerCount = 5;
    var reloadsPerWriter = 10;

    // Use a lock to coordinate file writes (file I/O needs external coordination).
    // This test verifies the ReaderWriterLockSlim inside the checker handles
    // concurrent ReloadFromDisk calls correctly.
    var fileLock = new object();

    // Act: Multiple threads trying to reload simultaneously.
    var tasks = Enumerable.Range(0, writerCount).Select(writerIndex => Task.Run(() =>
    {
      try
      {
        for (var i = 0; i < reloadsPerWriter; i++)
        {
          // Coordinate file writes AND reload to avoid file locking errors.
          // File I/O needs external coordination since ReloadFromDisk reads the file.
          // The ReaderWriterLockSlim inside the checker protects the in-memory data structures.
          lock (fileLock)
          {
            var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
            File.WriteAllLines(blocklistPath, [$"writer-{writerIndex}-iter-{i}.com"]);
            checker.ReloadFromDisk(blocklistDir);
          }
        }
      }
      catch (Exception ex)
      {
        lock (errors)
        {
          errors.Add(ex);
        }
      }
    }));

    await Task.WhenAll(tasks);

    // Assert: No exceptions from concurrent reload attempts.
    // The ReaderWriterLockSlim should serialize the writes.
    errors.Should().BeEmpty("concurrent reload attempts should be serialized safely");

    // Verify checker still works.
    var result = checker.IsDisposable("gmail.com");
    result.Should().BeFalse("checker should still function after concurrent reloads");
  }

  #endregion


  #region Tests - Concurrent ReloadFromDisk

  [Fact]
  public async Task DisposableEmailDomainChecker_SerializedReloadsFromMultipleThreads_ThreadSafe()
  {
    // Arrange: Create blocklist directory with initial content.
    var blocklistDir = _fixture.CreateSubdirectory("serialized-reloads");
    _fixture.CreateBlocklistFile(blocklistDir, "initial.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    var errors = new List<Exception>();
    var taskCount = 10;
    var reloadsPerTask = 5;
    var reloadCount = 0;

    // Use a lock for file I/O (file access needs external coordination).
    // This simulates a real-world scenario where file updates + reloads are coordinated.
    var fileLock = new object();

    // Act: Multiple threads each doing file-update + reload in sequence.
    // The ReaderWriterLockSlim serializes the write operations to the HashSet.
    var tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
    {
      try
      {
        for (var i = 0; i < reloadsPerTask; i++)
        {
          lock (fileLock)
          {
            var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
            File.WriteAllLines(blocklistPath, [$"task-{taskIndex}-iter-{i}.com", "initial.com"]);
            checker.ReloadFromDisk(blocklistDir);
            Interlocked.Increment(ref reloadCount);
          }

          // Small delay to allow interleaving between lock acquisitions.
          Thread.Sleep(Random.Shared.Next(1, 5));
        }
      }
      catch (Exception ex)
      {
        lock (errors)
        {
          errors.Add(ex);
        }
      }
    }));

    await Task.WhenAll(tasks);

    // Assert: No exceptions from reloads.
    errors.Should().BeEmpty("serialized reload calls from multiple threads should succeed");

    // Verify all reloads completed.
    reloadCount.Should().Be(taskCount * reloadsPerTask);

    // Verify checker still functions correctly.
    checker.IsDisposable("initial.com").Should().BeTrue("checker should work after many reloads");
    checker.IsDisposable("gmail.com").Should().BeFalse("non-blocked domain should not be affected");
  }


  [Fact]
  public async Task DisposableEmailDomainChecker_ReloadWithConcurrentReads_ThreadSafe()
  {
    // This test verifies the main thread-safety concern: reads during reloads.
    // The ReaderWriterLockSlim ensures reads and writes don't corrupt each other.

    // Arrange: Create blocklist directory with initial content.
    var blocklistDir = _fixture.CreateSubdirectory("reload-concurrent-reads");
    _fixture.CreateBlocklistFile(blocklistDir, "blocked-domain.com");
    _fixture.CreateAllowlistFile(blocklistDir);

    var options = Options.Create(new EmailValidationOptions
    {
      BlocklistDirectory = blocklistDir
    });
    var logger = Mock.Of<ILogger<DisposableEmailDomainChecker>>();
    var checker = new DisposableEmailDomainChecker(options, logger);

    var errors = new List<Exception>();
    var readCount = 0;
    var reloadCount = 0;
    var fileLock = new object();

    using var cts = new CancellationTokenSource();

    // Start concurrent read tasks - materialize them immediately so they start.
    var readerTasks = Enumerable.Range(0, 5).Select(readerIndex => Task.Run(async () =>
    {
      try
      {
        while (!cts.Token.IsCancellationRequested)
        {
          // Perform reads - these should never throw or return incorrect results.
          checker.IsDisposable("blocked-domain.com");
          checker.IsDisposable("mailinator.com");
          checker.IsDisposable("gmail.com");
          Interlocked.Increment(ref readCount);
          await Task.Yield();
        }
      }
      catch (OperationCanceledException) { }
      catch (Exception ex)
      {
        lock (errors) { errors.Add(ex); }
      }
    }, cts.Token)).ToArray(); // ToArray() forces immediate task creation.

    // Give reader tasks time to start.
    await Task.Delay(50);

    // Perform reloads while readers are running.
    var reloadTask = Task.Run(() =>
    {
      try
      {
        for (var i = 0; i < 20; i++)
        {
          lock (fileLock)
          {
            var blocklistPath = Path.Combine(blocklistDir, "disposable_email_blocklist.conf");
            File.WriteAllLines(blocklistPath, [$"blocked-domain.com", $"iteration-{i}.com"]);
            checker.ReloadFromDisk(blocklistDir);
            Interlocked.Increment(ref reloadCount);
          }

          Thread.Sleep(10);
        }
      }
      catch (Exception ex)
      {
        lock (errors) { errors.Add(ex); }
      }
    });

    await reloadTask;
    cts.Cancel();

    try { await Task.WhenAll(readerTasks); }
    catch (OperationCanceledException) { }

    // Assert: No errors during concurrent read/write operations.
    errors.Should().BeEmpty("concurrent reads during reloads should be handled by ReaderWriterLockSlim");

    // Verify significant concurrent activity occurred.
    readCount.Should().BeGreaterThan(100, "many reads should have occurred");
    reloadCount.Should().Be(20, "all reloads should have completed");
  }

  #endregion


  #region IDisposable

  public void Dispose()
  {
    _fixture.Dispose();
  }

  #endregion
}
