# Mocks

Use this folder for reusable test doubles shared by multiple tests or suites.

## Purpose

This folder is for:

- `Fake` implementations
- `Stub` services
- `Spy` recorders
- thin wrappers around mocking frameworks when reuse is real

The goal is to keep tests readable without rebuilding the same doubles everywhere.

## Terminology

- `Fake`: working in-memory implementation with simplified behavior
- `Stub`: predefined return values, minimal behavior
- `Spy`: records calls so the test can assert what happened
- `Mock`: behavior-verified double, often from Moq

Prefer concrete fakes and spies when they improve clarity more than an inline `Mock<T>`.

## Current repo examples

Relevant existing doubles live today in integration support:

- `InMemoryUserRepository`
- `NoOpEmailService`
- `FakeAuthService`

Those are good candidates to stay suite-local or be promoted here if they become genuinely cross-suite.

## What belongs here

Examples:

- `FakeEmailService`
- `FakeClock`
- `SpyHubContext`
- `StubAuthorizationService`
- `InMemoryResetTokenStore`

## What does not belong here

Avoid putting here:

- one-off `Mock<T>` setup used by a single test
- giant helper methods that hide the behavior under test
- mocks with scenario-specific assertions embedded inside them

## Example fake

```csharp
internal sealed class FakeEmailService : IEmailService
{
    public List<(string To, string Subject)> Sent { get; } = new();

    public Task SendWelcomeEmailAsync(string email, string displayName)
    {
        Sent.Add((email, "welcome"));
        return Task.CompletedTask;
    }

    public Task SendResetEmailAsync(string email, string displayName, string resetLink)
    {
        Sent.Add((email, "reset"));
        return Task.CompletedTask;
    }
}
```

This is often easier to read than setting up multiple `Mock<IEmailService>` expectations in every test.

## Example spy

```csharp
internal sealed class SpyHubClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> Calls { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Calls.Add((method, args));
        return Task.CompletedTask;
    }
}
```

## Rules

- keep behavior minimal
- make captured state easy to assert
- avoid randomness
- do not silently swallow important test signals unless that is the explicit purpose

## Suggested next concrete additions

Useful shared doubles for this repo:

- `FakeEmailService`
- `FakeDateTimeProvider`
- `SpyHubContext`
- `StubAuthorizationService`
- `InMemoryResetTokenStore`
