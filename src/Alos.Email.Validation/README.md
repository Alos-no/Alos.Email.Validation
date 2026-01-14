# Alos.Email.Validation

Email validation library for anti-abuse protection in SaaS applications.

## Features

- **Provider-Specific Normalization** - Gmail dots/plus, Outlook plus, ProtonMail plus, etc.
- **Relay Service Blocking** - Apple Hide My Email, Firefox Relay, DuckDuckGo, SimpleLogin, etc.
- **Disposable Domain Detection** - 38,000+ known disposable email providers
- **MX Record Verification** - DNS-based domain validation with fail-open strategy
- **Custom Blocklist/Allowlist** - Override built-in lists with your own domains
- **Hot-Reload** - Runtime blocklist updates without redeployment
- **Auto-Update** - Background service for periodic blocklist updates from GitHub
- **Thread-Safe** - All operations are safe for concurrent access

## Installation

```bash
dotnet add package Alos.Email.Validation
```

## Quick Start

```csharp
// Register services
builder.Services.AddEmailValidation(builder.Configuration);

// Or with auto-update
builder.Services.AddEmailValidationWithAutoUpdate(builder.Configuration);
```

```json
{
  "EmailValidation": {
    "BlocklistDirectory": "/var/lib/alos/email-blocklists",
    "EnableAutoUpdate": true,
    "UpdateInterval": "1.00:00:00",
    "InitialUpdateDelay": "00:00:30",
    "CustomBlocklist": ["spammer.com"],
    "CustomAllowlist": ["legitimate-service.com"],
    "CustomBlocklistUrls": ["https://example.com/my-blocklist.txt"],
    "CustomAllowlistUrls": ["https://example.com/my-allowlist.txt"]
  }
}
```

## Usage

```csharp
public class RegistrationService(IEmailValidationService emailValidator)
{
    public async Task<bool> RegisterAsync(string email, CancellationToken ct)
    {
        // Full async validation (includes MX check)
        var result = await emailValidator.ValidateAsync(email, ct);
        if (!result.IsValid)
        {
            // result.Error: InvalidFormat, RelayService, Disposable, InvalidDomain
            return false;
        }

        // Or use individual checks
        if (emailValidator.IsRelayService(email))
            return false; // Relay services not allowed

        if (emailValidator.IsDisposable(email))
            return false; // Disposable emails not allowed

        // Get normalized email for uniqueness checks
        var normalized = emailValidator.Normalize(email);

        // Continue with registration...
        return true;
    }
}
```

## Components

| Component | Description |
|-----------|-------------|
| `IEmailValidationService` | Orchestrates all validation checks |
| `IDisposableEmailDomainChecker` | Disposable domain detection with hot-reload |
| `IMxRecordValidator` | DNS MX record verification |
| `EmailNormalizer` | Static class for provider-specific email normalization |
| `RelayServiceBlocklist` | Static class blocking 21+ relay/forwarding services |

## Framework Support

- .NET 8.0
- .NET 9.0
- .NET 10.0

## License

Apache-2.0 License
