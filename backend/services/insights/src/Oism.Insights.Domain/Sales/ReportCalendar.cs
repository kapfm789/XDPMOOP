namespace Oism.Insights.Domain.Sales;

// Ngày báo cáo cắt theo giờ Việt Nam (UTC+7, không có giờ mùa hè); dữ liệu lưu UTC (ADR-0008).
public static class ReportCalendar
{
    public const string TimeZone = "Asia/Ho_Chi_Minh";

    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly DateOf(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(Offset).DateTime);

    // 00:00 giờ Việt Nam của ngày đó.
    public static DateTimeOffset StartOf(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), Offset);
}
