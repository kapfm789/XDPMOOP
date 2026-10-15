using Oism.Insights.Domain.Sales;

namespace Oism.Insights.UnitTests.Reports;

// ADR-0008: ngày báo cáo cắt theo giờ Việt Nam (UTC+7); dữ liệu lưu UTC.
public sealed class ReportCalendarTests
{
    [Theory]
    [InlineData("2026-10-20T16:59:59Z", "2026-10-20")]
    [InlineData("2026-10-20T17:00:00Z", "2026-10-21")]
    [InlineData("2026-10-21T00:30:00+07:00", "2026-10-21")]
    public void DateOf_UtcInstant_VietnamDate(string instant, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), ReportCalendar.DateOf(DateTimeOffset.Parse(instant)));

    [Fact]
    public void StartOf_Date_MidnightVietnamTime() =>
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T17:00:00Z"), ReportCalendar.StartOf(new DateOnly(2026, 10, 21)));

    // "30 ngày gần nhất" gồm hôm nay theo giờ Việt Nam và 29 ngày trước đó.
    [Theory]
    [Trait("UseCase", "UC-REP-02 AC-2")]
    [InlineData("2026-10-21T03:00:00Z", 30, "2026-09-21T17:00:00Z")]
    [InlineData("2026-10-20T17:30:00Z", 30, "2026-09-21T17:00:00Z")]
    [InlineData("2026-10-21T03:00:00Z", 1, "2026-10-20T17:00:00Z")]
    public void StartOfLastDays_NowAndDays_MidnightOfFirstVietnamDay(string now, int days, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), ReportCalendar.StartOfLastDays(DateTimeOffset.Parse(now), days));
}
