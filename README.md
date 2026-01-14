# Alos.Email.Validation

A comprehensive .NET library for email validation and anti-abuse protection. Includes provider-specific normalization, relay service blocking, disposable domain detection, and MX record verification.

[![NuGet](https://img.shields.io/nuget/v/Alos.Email.Validation.svg)](https://www.nuget.org/packages/Alos.Email.Validation)

## Features

- **Email Normalization** - Provider-specific rules for Gmail, Outlook, ProtonMail, iCloud, and more
- **Relay Service Detection** - Block Apple Hide My Email, Firefox Relay, DuckDuckGo, SimpleLogin, etc.
- **Disposable Domain Detection** - 38,000+ domains from [disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains)
- **MX Record Verification** - DNS lookup to verify domain can receive email
- **Custom Blocklist/Allowlist** - Override built-in lists with your own domains
- **Auto-Update** - Background service to keep blocklists current
- **Thread-Safe** - All operations are safe for concurrent access

## Installation

```bash
dotnet add package Alos.Email.Validation
```

## Quick Start

```csharp
// Program.cs
builder.Services.AddEmailValidation(builder.Configuration);

// In your service
public class MyService
{
    private readonly IEmailValidationService _emailValidation;

    public MyService(IEmailValidationService emailValidation)
    {
        _emailValidation = emailValidation;
    }

    public async Task<bool> RegisterUserAsync(string email)
    {
        var result = await _emailValidation.ValidateAsync(email);

        if (!result.IsValid)
        {
            // Handle validation error
            switch (result.Error)
            {
                case EmailValidationError.InvalidFormat:
                    // Email format is invalid
                    break;
                case EmailValidationError.RelayService:
                    // Email uses a relay service (Apple, Firefox, etc.)
                    break;
                case EmailValidationError.Disposable:
                    // Email uses a disposable domain
                    break;
                case EmailValidationError.InvalidDomain:
                    // Domain has no MX records
                    break;
            }
            return false;
        }

        // Normalize email before storing (prevents alias duplicates)
        var normalizedEmail = _emailValidation.Normalize(email);

        // Continue with registration...
        return true;
    }
}
```

## Configuration

### Basic Setup

```csharp
// Using IConfiguration (recommended)
builder.Services.AddEmailValidation(builder.Configuration);

// Or programmatic configuration
builder.Services.AddEmailValidation(options =>
{
    options.CustomBlocklist = ["spammer.com", "badactor.net"];
    options.CustomAllowlist = ["legitimate-service.com"];
});
```

### Configuration Options

```json
{
  "EmailValidation": {
    "EnableAutoUpdate": true,
    "UpdateInterval": "1.00:00:00",
    "CustomBlocklist": ["spammer.com", "badactor.net"],
    "CustomAllowlist": ["legitimate-service.com"],
    "CustomBlocklistUrls": ["https://example.com/my-blocklist.txt"],
    "CustomAllowlistUrls": ["https://example.com/my-allowlist.txt"]
  }
}
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BlocklistDirectory` | `string?` | Cross-platform default | Path to blocklist files. Defaults to `LocalApplicationData/alos/email-blocklists`. |
| `EnableAutoUpdate` | `bool` | `false` | Enable background blocklist updates from GitHub. |
| `UpdateInterval` | `TimeSpan` | `24:00:00` | Interval between update checks. |
| `BlocklistUrl` | `string` | GitHub URL | URL for primary blocklist download. |
| `AllowlistUrl` | `string` | GitHub URL | URL for primary allowlist download. |
| `CustomBlocklistUrls` | `List<string>` | `[]` | Additional URLs for custom blocklists (fetched by auto-updater). |
| `CustomAllowlistUrls` | `List<string>` | `[]` | Additional URLs for custom allowlists (fetched by auto-updater). |
| `CustomBlocklist` | `List<string>` | `[]` | Inline domains to add to blocklist. |
| `CustomAllowlist` | `List<string>` | `[]` | Inline domains to add to allowlist (overrides blocklist). |

### Auto-Update Setup

Enable automatic blocklist updates from GitHub:

```csharp
builder.Services.AddEmailValidationWithAutoUpdate(builder.Configuration);
```

This registers a background service that periodically downloads the latest blocklists. Requires `BlocklistDirectory` to be set and `EnableAutoUpdate` to be `true`.

## Email Normalization

The library normalizes email addresses using provider-specific rules to prevent alias-based duplicate accounts:

| Provider | Normalization | Example |
|----------|---------------|---------|
| **Gmail** | Remove dots and +suffix | `j.doe+spam@gmail.com` → `jdoe@gmail.com` |
| **Outlook/Hotmail** | Remove +suffix only | `john+tag@outlook.com` → `john@outlook.com` |
| **ProtonMail** | Remove +suffix only | `user+test@proton.me` → `user@proton.me` |
| **iCloud** | Remove +suffix only | `user+tag@icloud.com` → `user@icloud.com` |
| **Yahoo** | No normalization | Yahoo aliases are pre-created, not spontaneous |
| **Other** | Lowercase only | Preserves address as-is |

```csharp
var normalized = _emailValidation.Normalize("J.Doe+Spam@GMAIL.com");
// Returns: "jdoe@gmail.com"
```

## Relay Service Detection

Blocks email relay/forwarding services that enable unlimited alias creation:

- **Apple** - Hide My Email (`privaterelay.appleid.com`)
- **Mozilla** - Firefox Relay (`mozmail.com`)
- **DuckDuckGo** - Email Protection (`duck.com`)
- **SimpleLogin** - Proton-owned (`simplelogin.com`, `slmails.com`, etc.)
- **Proton Pass** - (`passmail.net`)
- **addy.io** - AnonAddy (`addy.io`, `anonaddy.com`, etc.)
- **Fastmail** - Masked Email (`fastmail.com`)
- **Cloaked** - (`cloaked.id`, `myclkd.email`)
- **Burner Mail** - (`nicoric.com`)
- **GitHub** - (`users.noreply.github.com`)

Includes wildcard matching for subdomain-based services (e.g., `alias@username.anonaddy.com`).

```csharp
bool isRelay = _emailValidation.IsRelayService("user@duck.com");
// Returns: true
```

## Disposable Domain Detection

Detects 38,000+ disposable/temporary email domains using the community-maintained [disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains) list.

```csharp
bool isDisposable = _emailValidation.IsDisposable("user@mailinator.com");
// Returns: true
```

### Custom Lists

Override built-in detection with custom blocklists and allowlists:

```csharp
builder.Services.AddEmailValidation(options =>
{
    // Block additional domains
    options.CustomBlocklist = ["internal-temp.com", "competitor.com"];

    // Allow domains that appear on blocklist but are legitimate for your use case
    options.CustomAllowlist = ["temp-looks-suspicious-but-legit.com"];
});
```

**Precedence:** Allowlist always takes precedence over blocklist.

### URL-Based Custom Lists

For organization-specific or third-party blocklists that you want to keep synchronized, use URL-based configuration:

```csharp
builder.Services.AddEmailValidation(options =>
{
    options.EnableAutoUpdate = true;

    // Add custom blocklist URLs (fetched alongside primary lists)
    options.CustomBlocklistUrls = [
        "https://example.com/corporate-blocklist.txt",
        "https://internal.company.com/banned-domains.conf"
    ];

    // Add custom allowlist URLs (override false positives)
    options.CustomAllowlistUrls = [
        "https://example.com/trusted-partners.txt"
    ];
});
```

The auto-updater downloads all URLs and saves them to `BlocklistDirectory` with naming conventions:
- `custom_blocklist_000.conf`, `custom_blocklist_001.conf`, etc.
- `custom_allowlist_000.conf`, `custom_allowlist_001.conf`, etc.

### Local File-Based Custom Lists

You can also place custom list files directly in the `BlocklistDirectory`. Use the naming convention:

- `custom_blocklist_*.conf` - Custom blocklist files
- `custom_allowlist_*.conf` - Custom allowlist files

File format (one domain per line, `#` for comments):
```
# Corporate blocklist - Updated 2024-01
spammer.com
badactor.net
another-blocked.com
```

All files matching the naming convention are loaded and merged at startup and on hot reload.

## Hot Reload

Blocklists can be reloaded at runtime without restarting the application:

```csharp
// Inject the checker
private readonly IDisposableEmailDomainChecker _checker;

// Reload from disk (thread-safe)
_checker.ReloadFromDisk("/var/lib/alos/email-blocklists");
```

The auto-update background service calls this automatically after downloading new lists.

## API Reference

### IEmailValidationService

```csharp
public interface IEmailValidationService
{
    // Full validation pipeline (format, relay, disposable, MX)
    Task<EmailValidationResult> ValidateAsync(string email, CancellationToken ct = default);

    // Check if domain is a relay service (synchronous)
    bool IsRelayService(string email);

    // Check if domain is disposable (synchronous)
    bool IsDisposable(string email);

    // Normalize email using provider-specific rules
    string Normalize(string email);
}
```

### EmailValidationResult

```csharp
public sealed record EmailValidationResult
{
    public bool IsValid { get; init; }
    public EmailValidationError? Error { get; init; }
}

public enum EmailValidationError
{
    InvalidFormat,   // RFC 5322 format violation
    RelayService,    // Apple, Firefox, DuckDuckGo, etc.
    Disposable,      // Mailinator, Guerrilla Mail, etc.
    InvalidDomain    // No MX records
}
```

### Static Helpers

For scenarios where DI is not available:

```csharp
// Normalize without DI
string normalized = EmailNormalizer.Normalize("j.doe+spam@gmail.com");
string? domain = EmailNormalizer.ExtractDomain("user@example.com");

// Check relay service without DI
bool isRelay = RelayServiceBlocklist.IsRelayService("duck.com");
```

## Framework Support

- .NET 8.0
- .NET 9.0
- .NET 10.0

## Dependencies

- [DnsClient](https://www.nuget.org/packages/DnsClient) - MX record lookups
- Microsoft.Extensions.* - Configuration, DI, Hosting, Logging

## Thread Safety

All services are registered as singletons and are thread-safe:

- `IEmailValidationService` - Thread-safe validation
- `IDisposableEmailDomainChecker` - Uses `ReaderWriterLockSlim` for concurrent reads during hot reload
- `IMxRecordValidator` - Thread-safe DNS lookups

## Testing

The library includes comprehensive test coverage:

- **134 unit tests** - Core validation logic
- **39 integration tests** - File I/O, DI registration, hot reload, thread safety

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/Alos.Email.Validation.Tests/

# Run integration tests only
dotnet test tests/Alos.Email.Validation.IntegrationTests/
```

## License

MIT License - See [LICENSE](LICENSE) file for details.

## Credits

- Disposable domain list: [disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains)
- DNS client: [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET)
