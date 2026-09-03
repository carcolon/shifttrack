using ShiftTrack.Domain.Entities;
using ShiftTrack.Tests.Shared.Builders;
using Xunit;

namespace ShiftTrack.Api.Tests;

public class SchedulePeriodHelpersTests
{
    [Fact]
    public void ResolveSchedulePeriodForDate_RepeatsClosedCycleAfterLastPeriod()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithSchedulePeriods(
                new UserSchedulePeriodBuilder()
                    .WithUserId(userId)
                    .EffectiveFrom(new DateTime(2026, 3, 2))
                    .EffectiveTo(new DateTime(2026, 3, 8))
                    .WithShiftTime("Morning")
                    .WithBlocksJson("""[{"Start":"08:00","End":"17:00","Days":["Mon"]}]""")
                    .Repeating()
                    .Build(),
                new UserSchedulePeriodBuilder()
                    .WithId(Guid.NewGuid())
                    .WithUserId(userId)
                    .EffectiveFrom(new DateTime(2026, 3, 9))
                    .EffectiveTo(new DateTime(2026, 3, 15))
                    .WithShiftTime("Late")
                    .WithBlocksJson("""[{"Start":"10:00","End":"19:00","Days":["Tue"]}]""")
                    .Repeating()
                    .Build())
            .Build();

        var period = SchedulePeriodHelpers.ResolveSchedulePeriodForDate(user, new DateTime(2026, 3, 16));

        Assert.NotNull(period);
        Assert.Equal("Morning", period!.ShiftTime);
    }

    [Fact]
    public void BuildCalendarRow_UsesRepeatedPeriodBlocksForFutureCycle()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithSchedulePeriods(
                new UserSchedulePeriodBuilder()
                    .WithUserId(userId)
                    .EffectiveFrom(new DateTime(2026, 3, 2))
                    .EffectiveTo(new DateTime(2026, 3, 8))
                    .WithShiftTime("Morning")
                    .WithBlocksJson("""[{"Start":"08:00","End":"17:00","Days":["Mon"]}]""")
                    .Repeating()
                    .Build(),
                new UserSchedulePeriodBuilder()
                    .WithId(Guid.NewGuid())
                    .WithUserId(userId)
                    .EffectiveFrom(new DateTime(2026, 3, 9))
                    .EffectiveTo(new DateTime(2026, 3, 15))
                    .WithShiftTime("Late")
                    .WithBlocksJson("""[{"Start":"10:00","End":"19:00","Days":["Tue"]}]""")
                    .Repeating()
                    .Build())
            .Build();
        var days = Enumerable.Range(0, 7).Select(offset => new DateTime(2026, 3, 16).AddDays(offset));

        var row = CalendarHelpers.BuildCalendarRow(user, days, new Dictionary<string, UserScheduleOverride>());

        var monday = Assert.Single(row.Cells, cell => cell.Date == "2026-03-16");
        Assert.Equal("shiftMorning", monday.Type);
        Assert.Equal("08:00 - 17:00", monday.Label);
        Assert.Equal("Morning", row.ShiftTime);
    }

    [Fact]
    public void BuildCalendarRow_AppliesDailyOverrideOnlyToSelectedDate()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder()
            .WithId(userId)
            .WithShiftTime("Morning")
            .WithScheduleBlocks("""[{"Start":"08:00","End":"17:00","Days":["Mon","Tue","Wed","Thu","Fri"]}]""")
            .Build();
        var days = Enumerable.Range(0, 7).Select(offset => new DateTime(2026, 6, 22).AddDays(offset)).ToArray();
        var overrideDate = new DateTime(2026, 6, 24);
        var overrides = new Dictionary<string, UserScheduleOverride>
        {
            [$"{userId:N}|2026-06-24"] = new UserScheduleOverride
            {
                UserId = userId,
                OverrideDate = overrideDate,
                EntryType = "daily_schedule",
                RequestType = "shiftLate",
                StartTime = "13:30",
                EndTime = "22:30",
                Comments = "Temporary client coverage"
            }
        };

        var row = CalendarHelpers.BuildCalendarRow(user, days, overrides);

        Assert.Equal("08:00 - 17:00", Assert.Single(row.Cells, cell => cell.Date == "2026-06-23").Label);
        var changedDay = Assert.Single(row.Cells, cell => cell.Date == "2026-06-24");
        Assert.Equal("13:30 - 22:30", changedDay.Label);
        Assert.Equal("shiftLate", changedDay.Type);
        Assert.True(changedDay.IsDailyScheduleOverride);
        Assert.Equal("Temporary client coverage", changedDay.ScheduleOverrideComments);
        Assert.Equal("08:00 - 17:00", Assert.Single(row.Cells, cell => cell.Date == "2026-06-25").Label);
    }

    [Fact]
    public void MergeSchedulePeriods_TruncatesExistingClosedPeriod_WhenIncomingStartsInsideIt()
    {
        var userId = Guid.NewGuid();
        var existing = new[]
        {
            new UserSchedulePeriodBuilder()
                .WithUserId(userId)
                .EffectiveFrom(new DateTime(2026, 9, 1))
                .EffectiveTo(new DateTime(2026, 9, 15))
                .WithShiftTime("Morning")
                .WithBlocksJson("""[{"Start":"08:00","End":"17:00","Days":["Mon"]}]""")
                .Build()
        };
        var incoming = new[]
        {
            new UserSchedulePeriodBuilder()
                .WithUserId(userId)
                .EffectiveFrom(new DateTime(2026, 9, 14))
                .WithShiftTime("Late")
                .WithBlocksJson("""[{"Start":"13:00","End":"22:00","Days":["Mon"]}]""")
                .Build()
        };

        var merged = BulkUserUploadHelpers.MergeSchedulePeriods(userId, existing, incoming);

        Assert.Collection(
            merged,
            period =>
            {
                Assert.Equal(new DateTime(2026, 9, 1), period.EffectiveFrom);
                Assert.Equal(new DateTime(2026, 9, 13), period.EffectiveTo);
                Assert.Equal("Morning", period.ShiftTime);
            },
            period =>
            {
                Assert.Equal(new DateTime(2026, 9, 14), period.EffectiveFrom);
                Assert.Null(period.EffectiveTo);
                Assert.Equal("Late", period.ShiftTime);
            });
    }

    [Fact]
    public void MergeSchedulePeriods_SplitsExistingOpenPeriod_WhenIncomingHasClosedRange()
    {
        var userId = Guid.NewGuid();
        var existing = new[]
        {
            new UserSchedulePeriodBuilder()
                .WithUserId(userId)
                .EffectiveFrom(new DateTime(2026, 9, 1))
                .WithShiftTime("Morning")
                .WithBlocksJson("""[{"Start":"08:00","End":"17:00","Days":["Mon"]}]""")
                .Build()
        };
        var incoming = new[]
        {
            new UserSchedulePeriodBuilder()
                .WithUserId(userId)
                .EffectiveFrom(new DateTime(2026, 9, 14))
                .EffectiveTo(new DateTime(2026, 9, 20))
                .WithShiftTime("Late")
                .WithBlocksJson("""[{"Start":"13:00","End":"22:00","Days":["Mon"]}]""")
                .Build()
        };

        var merged = BulkUserUploadHelpers.MergeSchedulePeriods(userId, existing, incoming);

        Assert.Collection(
            merged,
            period =>
            {
                Assert.Equal(new DateTime(2026, 9, 1), period.EffectiveFrom);
                Assert.Equal(new DateTime(2026, 9, 13), period.EffectiveTo);
                Assert.Equal("Morning", period.ShiftTime);
            },
            period =>
            {
                Assert.Equal(new DateTime(2026, 9, 14), period.EffectiveFrom);
                Assert.Equal(new DateTime(2026, 9, 20), period.EffectiveTo);
                Assert.Equal("Late", period.ShiftTime);
            },
            period =>
            {
                Assert.Equal(new DateTime(2026, 9, 21), period.EffectiveFrom);
                Assert.Null(period.EffectiveTo);
                Assert.Equal("Morning", period.ShiftTime);
            });
    }
}
