# Builders

Use builders when test setup becomes noisy, repetitive, or easy to get wrong.

## Purpose

Builders should help tests express intent like:

- "employee with a morning shift"
- "manager with a future effective period"
- "swap request already approved"
- "PTO request pending review"

Instead of forcing every test to manually initialize every property.

## When to add a builder

Create a builder when:

- the same object initializer appears in multiple tests
- the object has many required fields
- the scenario depends on a small subset of fields changing
- the test becomes harder to scan because setup dominates the file

Do not create a builder when:

- the object is tiny
- it is used in only one place
- an inline initializer is clearer

## Builder style

Prefer fluent builders with safe defaults:

```csharp
var user = new UserBuilder()
    .AsEmployee()
    .WithName("Charlie Colon")
    .WithEmail("charlie.colon@solvoglobal.com")
    .Build();
```

Defaults should be:

- valid
- unsurprising
- deterministic

Avoid defaults that silently encode business decisions that the test is supposed to declare explicitly.

## Recommended builders for this repo

High-value candidates:

- `UserBuilder`
- `SchedulePeriodBuilder`
- `CalendarRowBuilder`
- `PtoRequestBuilder`
- `SwapRequestBuilder`
- `ResetPasswordRequestBuilder`

## Example shape

```csharp
internal sealed class UserBuilder
{
    private readonly User _user = new()
    {
        Id = 1,
        Name = "Test User",
        Email = "test.user@solvoglobal.com",
        Role = 0,
        IsActive = true
    };

    public UserBuilder AsEmployee()
    {
        _user.Role = 0;
        return this;
    }

    public UserBuilder AsManager()
    {
        _user.Role = 1;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _user.Email = email;
        return this;
    }

    public User Build() => _user with { };
}
```

## Rules for good builders

- one `Build()` method
- explicit `With...` methods
- role/state convenience methods are fine
- avoid hidden random values
- avoid time-sensitive defaults unless frozen
- clone or create new output so one test does not mutate another test's object

## Anti-patterns

Avoid:

- builders that hit the database
- builders that depend on external config
- builders that perform assertions
- builders that automatically create whole object graphs without the test realizing it

## Suggested next concrete additions

The current tests would benefit from:

- `UserBuilder` for unit tests using repeated email/role setups
- `SwapRequestBuilder` for new change-request flows
- `SchedulePeriodBuilder` for multi-period scheduling tests
- `CalendarEntryBuilder` for readable schedule-day state combinations
