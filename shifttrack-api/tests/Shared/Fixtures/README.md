# Fixtures

Fixtures are reusable sample inputs or serialized payloads used by multiple tests.

## Purpose

Use fixtures for data that should stay stable across runs, such as:

- request payloads
- response snapshots
- serialized JSON bodies
- seeded users and schedule states
- sample swap and PTO scenarios

Fixtures help reduce:

- giant string literals embedded in tests
- duplicated sample data
- fragile copy/paste payloads

## What belongs here

Good fixtures:

- `pending-pto-request.json`
- `approved-cross-date-swap.json`
- `calendar-week-with-risk.json`
- `employee-with-multi-period-schedule.json`

Bad fixtures:

- huge dumps where nobody knows what matters
- environment-specific secrets
- data that changes every test run

## Format guidance

Prefer:

- small, scenario-focused files
- human-readable names
- only the fields the scenario really needs

Suggested structure:

```text
Fixtures/
  Calendar/
    week-with-coverage-risk.json
    week-with-pto-overlap.json
  PTO/
    pending-request.json
    approved-request.json
  Swaps/
    pending-same-role-swap.json
    approved-cross-date-swap.json
  Users/
    active-employee.json
    inactive-manager.json
```

## How tests should use fixtures

Keep loading explicit:

```csharp
var json = File.ReadAllText("Fixtures/Swaps/approved-cross-date-swap.json");
var request = JsonSerializer.Deserialize<SwapRequestDto>(json);
```

If the same loading code repeats, extract a small fixture loader helper, but keep the fixture names visible in the test.

## Rules

- fixtures must be deterministic
- fixtures must be committed to source control
- fixtures should be minimal
- fixture names should describe the scenario, not the transport format only

## When not to use a fixture

Do not use a fixture if:

- the data is shorter and clearer inline
- the scenario needs only one or two fields
- the fixture hides essential business meaning from the test

## Suggested next concrete additions

Useful fixtures for this repo:

- swap request email payload examples
- PTO review payloads
- calendar export sample rows
- assistant query samples in English and Spanish
- schedule period chains with effective from / effective to transitions
