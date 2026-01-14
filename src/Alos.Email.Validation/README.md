# Alos.Email.Validation

Email validation library for anti-abuse protection in SaaS applications.

## Features

- **Format Validation** - HTML5 Living Standard compliant email format validation
- **Provider-Specific Normalization** - Gmail dots/plus, Outlook plus, ProtonMail plus, etc.
- **Relay Service Blocking** - Apple Hide My Email, Firefox Relay, DuckDuckGo, SimpleLogin, etc.
- **Disposable Domain Detection** - 38,000+ known disposable email providers
- **MX Record Verification** - DNS-based domain validation with fail-open strategy
- **MX Whitelist** - Bypass MX validation for test/development domains
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
    "CustomAllowlistUrls": ["https://example.com/my-allowlist.txt"],
    "WhitelistedMxDomains": ["test.local", "itest.alos.local"]
  }
}
```

## API Reference

### IEmailValidationService

| Method | Description |
|--------|-------------|
| `ValidateEmailAsync(email, ct)` | Full async validation: format + relay + disposable + MX |
| `ValidateMxAsync(email, ct)` | MX record validation only (use when other checks done separately) |
| `ValidateFormat(email)` | Synchronous HTML5 format validation |
| `IsRelayService(email)` | Check if email uses a relay/forwarding service |
| `IsDisposable(email)` | Check if email domain is disposable |
| `Normalize(email)` | Get provider-normalized form for uniqueness checks |

### Usage Patterns

#### Full Validation (All-in-One)

```csharp
public class RegistrationService(IEmailValidationService emailValidator)
{
    public async Task<bool> RegisterAsync(string email, CancellationToken ct)
    {
        // Full async validation (format + relay + disposable + MX)
        var result = await emailValidator.ValidateEmailAsync(email, ct);
        if (!result.IsValid)
        {
            // result.Error: InvalidFormat, RelayService, Disposable, InvalidDomain
            return false;
        }

        // Get normalized email for uniqueness checks
        var normalized = emailValidator.Normalize(email);

        // Continue with registration...
        return true;
    }
}
```

#### With FluentValidation (Split Validation)

When using FluentValidation for synchronous checks, use `ValidateMxAsync` in your handler
to avoid duplicating validation:

```csharp
// Validator (synchronous checks)
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator(IEmailValidationService emailValidator)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .Must(emailValidator.ValidateFormat).WithMessage("Invalid email format")
            .Must(e => !emailValidator.IsRelayService(e)).WithMessage("Relay services not allowed")
            .Must(e => !emailValidator.IsDisposable(e)).WithMessage("Disposable emails not allowed");
    }
}

// Handler (async MX check only)
public class RegisterCommandHandler(IEmailValidationService emailValidator)
{
    public async Task<Result> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        // MX validation only (format/relay/disposable already validated)
        var mxResult = await emailValidator.ValidateMxAsync(cmd.Email, ct);
        if (!mxResult.IsValid)
            return Result.Failure(Errors.InvalidDomain);

        // Continue with registration...
    }
}
```

#### Individual Checks

```csharp
// Format validation (synchronous)
if (!emailValidator.ValidateFormat(email))
    return Error("Invalid email format");

// Relay service check
if (emailValidator.IsRelayService(email))
    return Error("Relay services not allowed");

// Disposable domain check
if (emailValidator.IsDisposable(email))
    return Error("Disposable emails not allowed");

// Normalization for uniqueness
var normalized = emailValidator.Normalize(email);
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `BlocklistDirectory` | string | null | Directory for blocklist files |
| `EnableAutoUpdate` | bool | false | Enable background blocklist updates |
| `UpdateInterval` | TimeSpan | 1 day | Interval between blocklist updates |
| `InitialUpdateDelay` | TimeSpan | 30s | Delay before first update |
| `CustomBlocklist` | string[] | [] | Additional domains to block |
| `CustomAllowlist` | string[] | [] | Domains to exclude from blocking |
| `CustomBlocklistUrls` | string[] | [] | URLs to fetch blocklists from |
| `CustomAllowlistUrls` | string[] | [] | URLs to fetch allowlists from |
| `WhitelistedMxDomains` | string[] | [] | Domains that bypass MX validation |

### WhitelistedMxDomains

Use this option to whitelist domains that should bypass MX validation (e.g., test domains
without real DNS records):

```json
{
  "EmailValidation": {
    "WhitelistedMxDomains": ["test.local", "itest.alos.local", "example.com"]
  }
}
```

Whitelisted domains:
- Return success from `ValidateMxAsync` without DNS lookup
- Return success from `ValidateEmailAsync` for MX check (other checks still apply)
- Are matched case-insensitively

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
