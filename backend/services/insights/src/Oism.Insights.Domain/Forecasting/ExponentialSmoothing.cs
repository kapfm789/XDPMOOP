namespace Oism.Insights.Domain.Forecasting;

// San bằng hàm mũ đơn (docs/design/forecast.md mục "Thuật toán"): L1 = y1, Lt = α·yt + (1 − α)·L(t−1).
public static class ExponentialSmoothing
{
    // Mức cuối L(n) của chuỗi, cũ trước mới sau. Chuỗi rỗng cho mức 0.
    public static decimal Level(IReadOnlyList<int> series, decimal alpha)
    {
        if (alpha is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "alpha must be in (0, 1]");
        if (series.Count == 0)
            return 0;

        decimal level = series[0];
        for (var t = 1; t < series.Count; t++)
            level = alpha * series[t] + (1 - alpha) * level;
        return level;
    }
}
