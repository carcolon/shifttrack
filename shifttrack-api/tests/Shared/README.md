# Shared Test Assets

`tests/Shared` is the common toolbox for test-only assets that are reused across more than one suite.

Use this area to avoid:

- copy/paste setup code across `Unit`, `Integration`, and `Acceptance`
- giant test methods full of object initialization noise
- one-off fake implementations duplicated in multiple projects
- inconsistent sample payloads for the same business scenario

This folder is intentionally split into:

- `Mocks`
- `Fixtures`
- `Builders`

## Design rules

Shared test assets should be:

- test-only
- deterministic
- lightweight
- explicit
- independent from environment secrets or live infrastructure

Shared test assets should not:

- depend on production configuration files
- hide business intent behind magic defaults
- become a second application layer
- contain assertions unrelated to their purpose

## What belongs here

Good candidates for `tests/Shared`:

- reusable fake implementations of interfaces
- seeded sample entities used in multiple suites
- request/response JSON payloads for endpoint tests
- fluent builders for verbose domain objects
- stable clock/date helpers for schedule and PTO scenarios
- common IDs, emails, names, and roles used repeatedly in tests

Bad candidates for `tests/Shared`:

- test logic that is only used once
- production helper code copied into tests
- giant fixture files that are harder to read than inline data
- mocks that assert scenario-specific behavior only one test needs

## Current repo alignment

Today the repo already contains reusable support in:

- `tests/Integration/ShiftTrack.Api.IntegrationTests/Support/TestApiFactory.cs`
- `tests/Integration/ShiftTrack.Api.IntegrationTests/Support/InMemoryUserRepository.cs`
- `tests/Integration/ShiftTrack.Api.IntegrationTests/Support/NoOpServices.cs`
- `tests/Integration/ShiftTrack.Api.IntegrationTests/Support/TestAuthHandler.cs`

Those files are valid reusable support, but over time some of their pieces may move into `tests/Shared` if they become cross-suite assets instead of integration-only assets.

Rule of thumb:

- keep suite-specific bootstrapping in the suite
- move generic reusable doubles, sample data, and builders into `tests/Shared`

## Suggested structure

Recommended evolution for this folder:

```text
tests/
  Shared/
    Builders/
      UserBuilder.cs
      SchedulePeriodBuilder.cs
      PtoRequestBuilder.cs
      SwapRequestBuilder.cs
    Fixtures/
      Users/
        active-manager.json
        employee-on-pto.json
      Calendar/
        week-with-coverage-risk.json
      Swaps/
        pending-cross-date-swap.json
    Mocks/
      FakeEmailService.cs
      FakeClock.cs
      StubAuthorizationService.cs
      SpyHubClientProxy.cs
```

Not every folder needs to exist immediately. Add files only when duplication appears in real tests.

## Ownership and boundaries

Use this decision model before adding anything here:

1. Is it reused by at least two tests?
2. Is it likely to be reused by more than one suite?
3. Does extracting it improve readability instead of hiding intent?

If the answer is not clearly yes, keep it local to the test file or suite for now.

## Naming conventions

Prefer these names:

- `FakeX` when it behaves like a simple in-memory implementation
- `StubX` when it returns fixed data
- `SpyX` when it records calls for assertions
- `TestXBuilder` or `XBuilder` for fluent object creation
- `sample-...json` or scenario-based fixture names for serialized payloads

Avoid generic names like:

- `Helpers.cs`
- `Utils.cs`
- `CommonStuff.cs`
- `Data.cs`

## Example workflow

When a unit test becomes noisy:

1. start with inline setup
2. extract repeated object creation into a builder
3. extract repeated fake behavior into a fake/stub
4. move it to `tests/Shared` only when reuse is real

Example:

```csharp
var user = new UserBuilder()
    .WithEmail("carlos.colon@solvoglobal.com")
    .AsEmployee()
    .WithMorningShift()
    .Build();
```

This is preferable to repeating a long object initializer in ten tests.

## Future additions worth creating next

High-value shared assets for this repo would likely be:

- `UserBuilder`
- `SchedulePeriodBuilder`
- `CalendarDayBuilder`
- `PtoRequestBuilder`
- `SwapRequestBuilder`
- `FakeEmailService`
- `FakeDateTimeProvider` or `FakeClock`
- common sample payloads for swap approval and PTO review flows

## Keep this folder honest

If a shared helper becomes too smart, split or simplify it.

The goal is:

- less duplication
- clearer tests
- faster onboarding

Not:

- hiding business rules in test infrastructure
