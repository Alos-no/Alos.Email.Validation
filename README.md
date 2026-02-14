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
        var result = await _emailValidation.ValidateEmailAsync(email);

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
        var normalizedEmail = EmailNormalizer.Normalize(email);

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
| **ProtonMail** | Remove dots, hyphens, underscores, and +suffix | `j.doe-test_name+spam@proton.me` → `jdoetestname@proton.me` |
| **Yahoo** | Remove -suffix (hyphen-based aliases, no plus support) | `john-shopping@yahoo.com` → `john@yahoo.com` |
| **Fastmail** | Remove +suffix; subdomain addressing | `alias@user.fastmail.com` → `user@fastmail.com` |
| **Outlook/Hotmail** | Remove +suffix only | `john+tag@outlook.com` → `john@outlook.com` |
| **iCloud** | Remove +suffix only | `user+tag@icloud.com` → `user@icloud.com` |
| **Yandex** | Remove +suffix only | `user+tag@yandex.ru` → `user@yandex.ru` |
| **GMX/mail.com** | Remove +suffix only | `user+tag@gmx.com` → `user@gmx.com` |
| **Tuta** | No normalization (no alias support) | `user+tag@tuta.com` → `user+tag@tuta.com` |
| **AOL** | No normalization (unclear plus support) | `user+tag@aol.com` → `user+tag@aol.com` |
| **QQ Mail** | No normalization (manual aliases only) | `user+tag@qq.com` → `user+tag@qq.com` |
| **NetEase** | No normalization (manual aliases only) | `user+tag@163.com` → `user+tag@163.com` |
| **Sina/Sohu/Aliyun** | No normalization | `user+tag@sina.com` → `user+tag@sina.com` |
| **Other** | Lowercase only (default) | `User+Spam@Example.com` → `user+spam@example.com` |

**Provider-Specific Notes:**

- **ProtonMail** ignores dots, hyphens, and underscores as a security measure against impersonation attacks
- **Yahoo** uses hyphen-based aliases (`nickname-keyword@yahoo.com`), not plus addressing
- **Fastmail** supports both plus addressing (`user+tag@fastmail.com`) and subdomain addressing (`anything@user.fastmail.com`)
- **Plus addressing providers**: Yandex, GMX, mail.com (200+ domains), Runbox, Mailfence, Rambler, Rackspace
- **No plus addressing**: Tuta/Tutanota, AOL/AIM (unclear support)
- **Chinese providers**: QQ Mail (qq.com, foxmail.com), NetEase (163.com, 126.com, yeah.net), Sina, Sohu, Aliyun use manual alias systems and do not support plus addressing
- **Unknown providers**: By default, plus suffix is preserved (conservative). Use `stripPlusForUnknownProviders: true` for aggressive anti-abuse mode.

```csharp
var normalized = EmailNormalizer.Normalize("J.Doe+Spam@GMAIL.com");
// Returns: "jdoe@gmail.com"

var protonNormalized = EmailNormalizer.Normalize("J.Doe-Test_Name+Tag@Proton.Me");
// Returns: "jdoetestname@proton.me"

var yahooNormalized = EmailNormalizer.Normalize("John-Shopping@Yahoo.com");
// Returns: "john@yahoo.com"

var fastmailSubdomain = EmailNormalizer.Normalize("Alias@User.Fastmail.Com");
// Returns: "user@fastmail.com"

var tutaNormalized = EmailNormalizer.Normalize("User+Tag@Tuta.com");
// Returns: "user+tag@tuta.com" (Tuta doesn't support plus addressing)

// Chinese providers: plus suffix preserved (no subaddressing support)
var qqNormalized = EmailNormalizer.Normalize("User+Tag@QQ.com");
// Returns: "user+tag@qq.com"

var neteaseNormalized = EmailNormalizer.Normalize("User+Tag@163.com");
// Returns: "user+tag@163.com"

// Unknown providers: default (conservative) preserves plus suffix
var unknownDefault = EmailNormalizer.Normalize("User+Spam@Company.com");
// Returns: "user+spam@company.com"

// Unknown providers: aggressive mode strips plus suffix
var unknownAggressive = EmailNormalizer.Normalize("User+Spam@Company.com", stripPlusForUnknownProviders: true);
// Returns: "user@company.com"
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
    Task<EmailValidationResult> ValidateEmailAsync(string email, CancellationToken ct = default);

    // MX record verification only (use when other checks already done)
    Task<EmailValidationResult> ValidateMxAsync(string email, CancellationToken ct = default);

    // Format validation using HTML5 Living Standard rules (synchronous)
    bool ValidateFormat(string email);

    // Check if domain is a relay service (synchronous)
    bool IsRelayService(string email);

    // Check if domain is disposable (synchronous)
    bool IsDisposable(string email);
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

### EmailNormalizer (Static)

Email normalization is provided exclusively via the static `EmailNormalizer` class:

```csharp
// Normalize email using provider-specific rules
string normalized = EmailNormalizer.Normalize("j.doe+spam@gmail.com");

// Extract domain from email
string? domain = EmailNormalizer.ExtractDomain("user@example.com");
```

### RelayServiceBlocklist (Static)

For relay service detection without DI:

```csharp
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

- **475+ unit tests** - Core validation logic, provider-specific normalization
- **80+ integration tests** - File I/O, DI registration, hot reload, thread safety

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/Alos.Email.Validation.Tests/

# Run integration tests only
dotnet test tests/Alos.Email.Validation.IntegrationTests/
```

## License

Apache License 2.0 - See [LICENSE](LICENSE) file for details.

## Credits

- Disposable domain list: [disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains)
- DNS client: [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET)
