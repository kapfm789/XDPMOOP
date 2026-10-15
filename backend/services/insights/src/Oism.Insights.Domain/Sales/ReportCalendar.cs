namespace Oism.Insights.Domain.Sales;

// Ngày báo cáo cắt theo giờ Việt Nam (UTC+7, không có giờ mùa hè); dữ liệu lưu UTC (ADR-0008).
public static class ReportCalendar
{
    public const string TimeZone = "Asia/Ho_Chi_Minh";

    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly DateOf(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(Offset).DateTime);

    // 00:00 giờ Việt Nam của ngày đó.
    public static DateTimeOffset StartOf(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), Offset);

    // "N ngày gần nhất" của bán chạy, bán chậm (ADR-0008): hôm nay theo giờ Việt Nam và N − 1 ngày trước đó.
    // Trả 00:00 giờ Việt Nam của ngày đầu cửa sổ; cửa sổ kéo tới thời điểm hiện tại.
    public static DateTimeOffset StartOfLastDays(DateTimeOffset now, int days) => StartOf(DateOf(now).AddDays(1 - days));
}
