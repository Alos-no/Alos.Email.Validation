// -----------------------------------------------------------------------------
// Alos.Email.Validation Sample Application
// -----------------------------------------------------------------------------
// This sample demonstrates how to integrate the Email Validation library
// into a .NET application using dependency injection.
// -----------------------------------------------------------------------------

using Alos.Email.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Build the host with Email Validation services.
var builder = Host.CreateApplicationBuilder(args);

// Register email validation services using configuration-based setup.
builder.Services.AddEmailValidation(builder.Configuration);

var host = builder.Build();

// Resolve the service and demonstrate usage.
using var scope = host.Services.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<IEmailValidationService>();

Console.WriteLine("=== Email Validation Sample ===");
Console.WriteLine();

// Test various email addresses.
var testEmails = new[]
{
  // Valid emails
  "john@gmail.com",
  "jane.doe+newsletter@outlook.com",

  // Disposable email domains
  "test@mailinator.com",
  "spam@guerrillamail.com",

  // Relay services (blocked)
  "alias@duck.com",
  "hidden@privaterelay.appleid.com",

  // Normalization examples
  "j.o.h.n.d.o.e@gmail.com",
  "JOHN+TAG@OUTLOOK.COM"
};

foreach (var email in testEmails)
{
  Console.WriteLine($"Testing: {email}");
  Console.WriteLine($"  Normalized: {service.Normalize(email)}");
  Console.WriteLine($"  Is Relay Service: {service.IsRelayService(email)}");
  Console.WriteLine($"  Is Disposable: {service.IsDisposable(email)}");

  var result = await service.ValidateAsync(email);
  Console.WriteLine($"  Validation Result: {(result.IsValid ? "Valid" : $"Invalid ({result.Error})")}");
  Console.WriteLine();
}

Console.WriteLine("=== Normalization Examples ===");
Console.WriteLine();

// Demonstrate provider-specific normalization.
var normalizationExamples = new (string Email, string Description)[]
{
  ("john.doe@gmail.com", "Gmail: dots are removed"),
  ("john+spam@gmail.com", "Gmail: plus suffix removed"),
  ("j.o.h.n+tag@gmail.com", "Gmail: both dots and plus"),
  ("john.doe@outlook.com", "Outlook: dots preserved"),
  ("john+tag@outlook.com", "Outlook: plus suffix removed"),
  ("john@yahoo.com", "Yahoo: no normalization"),
  ("john+tag@protonmail.com", "ProtonMail: plus suffix removed")
};

foreach (var (email, description) in normalizationExamples)
{
  var normalized = service.Normalize(email);
  Console.WriteLine($"  {email} -> {normalized}");
  Console.WriteLine($"    ({description})");
}

Console.WriteLine();
Console.WriteLine("Sample completed successfully.");
