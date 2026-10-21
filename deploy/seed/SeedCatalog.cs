namespace Oism.Seed;

// Hình dạng nhu cầu của một sản phẩm trong 60 ngày lịch sử, để báo cáo bán chạy, bán chậm và dự báo có gì để xem.
internal enum Demand
{
    Steady,
    Growing,
    Declining,
    // Mới bán 5 ngày gần nhất: dự báo hiện "chưa đủ dữ liệu".
    New,
    // Không bán được gì: đứng đầu báo cáo bán chậm.
    None,
}

// BankBin, BankAccount: tài khoản nhận chuyển khoản giả cho mã VietQR ở POS (W5-12); số tài khoản toàn số 0, không phải tài khoản thật.
internal sealed record BranchSeed(
    string Code, string Name, string Type, string Address, double DemandFactor, string? BankBin = null, string? BankAccount = null);

// Suffix ghép vào mã SKU; Attributes là thuộc tính biến thể. Sản phẩm không biến thể có đúng một Variant rỗng.
internal sealed record Variant(string Suffix, IReadOnlyDictionary<string, string> Attributes);

// DailyDemand là số bán trung bình mỗi ngày của cả sản phẩm tại một chi nhánh, chia đều cho các biến thể.
// LowStock: nhập ít và đặt ngưỡng cao hơn tồn để trang tổng quan có cảnh báo.
internal sealed record ProductSeed(
    string Code, string Name, string Category, string? Brand, decimal RetailPrice, decimal UnitCost, double DailyDemand,
    Demand Demand, IReadOnlyList<Variant> Variants, bool LowStock = false)
{
    public bool HasVariants => Variants.Count > 1 || Variants[0].Attributes.Count > 0;

    public string SkuCode(Variant variant) => variant.Suffix.Length == 0 ? Code : $"{Code}-{variant.Suffix}";
}

internal sealed record TenantSeed(
    string Key, string TenantName, string OwnerName, string OwnerEmail, string StaffName, string StaffEmail, string CashierName,
    string CashierEmail, string SupplierName, string ShopId, IReadOnlyList<BranchSeed> Branches, IReadOnlyList<ProductSeed> Products);

// Dữ liệu demo: 2 tenant, mỗi tenant 2 chi nhánh và khoảng 50 SKU (docs/plans/deploy.md, W4-09).
// Tên và thương hiệu là tên giả; email dùng miền .test.
internal static class SeedCatalog
{
    public static readonly IReadOnlyList<TenantSeed> Tenants =
    [
        new(
            Key: "moc",
            TenantName: "Thời trang Mộc",
            OwnerName: "Nguyễn Minh Anh",
            OwnerEmail: "owner@moc.test",
            StaffName: "Trần Thu Hà",
            StaffEmail: "staff@moc.test",
            CashierName: "Lê Văn Bình",
            CashierEmail: "cashier@moc.test",
            SupplierName: "Xưởng may Bình Minh",
            ShopId: "moc-shopee",
            Branches:
            [
                new("MOC-Q1", "Mộc Quận 1", "Store", "12 Lê Lợi, Quận 1, TP.HCM", 1.0, "970436", "0000000000"),
                new("MOC-Q3", "Mộc Quận 3", "Store", "88 Võ Văn Tần, Quận 3, TP.HCM", 0.7),
            ],
            Products:
            [
                P("AT-CT", "Áo thun cổ tròn", "Áo thun", "Mộc Basic", 150_000, 90_000, 6, Demand.Steady,
                    Grid(("Màu", ["Đen/DEN", "Trắng/TRANG"]), ("Size", ["S/S", "M/M", "L/L"]))),
                P("AT-PL", "Áo polo", "Áo thun", "Mộc Basic", 250_000, 150_000, 3, Demand.Growing,
                    Grid(("Màu", ["Navy/NAVY", "Trắng/TRANG"]), ("Size", ["M/M", "L/L"]))),
                P("SM-OX", "Áo sơ mi Oxford", "Áo sơ mi", "Mộc Studio", 350_000, 210_000, 2.5, Demand.Steady,
                    Grid(("Màu", ["Xanh/XANH", "Trắng/TRANG"]), ("Size", ["M/M", "L/L"]))),
                P("SM-CR", "Áo sơ mi caro", "Áo sơ mi", "Mộc Studio", 320_000, 190_000, 1, Demand.Declining,
                    Grid(("Size", ["M/M", "L/L"])), lowStock: true),
                P("QJ-SL", "Quần jean slim", "Quần", "Mộc Studio", 450_000, 270_000, 3, Demand.Steady,
                    Grid(("Size", ["29/29", "30/30", "31/31", "32/32"]))),
                P("QK-CL", "Quần kaki", "Quần", "Mộc Basic", 380_000, 230_000, 2, Demand.Steady,
                    Grid(("Màu", ["Be/BE", "Đen/DEN"]), ("Size", ["30/30", "32/32"]))),
                P("QS-TT", "Quần short thể thao", "Quần", "Mộc Basic", 180_000, 100_000, 3, Demand.Growing,
                    Grid(("Màu", ["Đen/DEN", "Xám/XAM"]), ("Size", ["M/M", "L/L"]))),
                P("AK-GI", "Áo khoác gió", "Áo khoác", "Mộc Studio", 550_000, 330_000, 1.5, Demand.Declining,
                    Grid(("Màu", ["Đen/DEN", "Rêu/REU"]), ("Size", ["M/M", "L/L"]))),
                P("AK-HD", "Áo hoodie", "Áo khoác", "Mộc Basic", 420_000, 250_000, 2, Demand.Steady,
                    Grid(("Màu", ["Xám/XAM", "Đen/DEN"]), ("Size", ["M/M", "L/L"]))),
                P("VA-MD", "Váy midi", "Váy", "Mộc Studio", 390_000, 230_000, 1.5, Demand.Steady,
                    Grid(("Mẫu", ["Hoa/HOA", "Trơn/TRON"]), ("Size", ["S/S", "M/M"]))),
                P("AL-MO", "Áo len mỏng", "Áo khoác", "Mộc Studio", 480_000, 290_000, 2, Demand.New,
                    Grid(("Size", ["M/M", "L/L"]))),
                P("PK-MU", "Mũ lưỡi trai", "Phụ kiện", "Mộc Basic", 120_000, 60_000, 2, Demand.Steady, Single()),
                P("PK-TL", "Thắt lưng da", "Phụ kiện", "Mộc Studio", 280_000, 150_000, 0.8, Demand.Steady, Single(), lowStock: true),
                P("PK-TA", "Tất cổ ngắn (3 đôi)", "Phụ kiện", "Mộc Basic", 60_000, 30_000, 4, Demand.Steady, Single()),
                P("PK-TO", "Túi tote canvas", "Phụ kiện", "Mộc Basic", 150_000, 80_000, 1, Demand.Growing, Single()),
                P("PK-KT", "Khẩu trang vải", "Phụ kiện", "Mộc Basic", 30_000, 12_000, 0, Demand.None, Single()),
            ]),
        new(
            Key: "xanh",
            TenantName: "Tạp hóa Xanh",
            OwnerName: "Phạm Quốc Huy",
            OwnerEmail: "owner@xanh.test",
            StaffName: "Đỗ Ngọc Lan",
            StaffEmail: "staff@xanh.test",
            CashierName: "Vũ Đức Nam",
            CashierEmail: "cashier@xanh.test",
            SupplierName: "Nhà phân phối Hòa Phát Đạt",
            ShopId: "xanh-shopee",
            Branches:
            [
                new("XANH-CG", "Xanh Cầu Giấy", "Store", "25 Xuân Thủy, Cầu Giấy, Hà Nội", 1.0, "970436", "0000000000"),
                new("XANH-DD", "Xanh Đống Đa", "Store", "140 Tây Sơn, Đống Đa, Hà Nội", 0.8),
            ],
            Products:
            [
                P("NS-500", "Nước suối", "Đồ uống", "Suối Mát", 6_000, 3_500, 14, Demand.Steady,
                    Grid(("Quy cách", ["Chai 500ml/C500", "Lốc 6 chai/L6"]))),
                P("NC-1L", "Nước cam ép 1L", "Đồ uống", "Vườn Xanh", 38_000, 26_000, 3, Demand.Growing, Single()),
                P("ST-KD", "Sữa tươi không đường", "Đồ uống", "Đồng Cỏ", 9_000, 6_500, 10, Demand.Steady,
                    Grid(("Quy cách", ["Hộp 180ml/H180", "Lốc 4 hộp/L4"]))),
                P("TX-450", "Trà xanh đóng chai 450ml", "Đồ uống", "Lá Biếc", 10_000, 6_800, 8, Demand.Steady, Single()),
                P("CF-LON", "Cà phê sữa lon", "Đồ uống", "Phố Núi", 15_000, 10_000, 5, Demand.Steady, Single()),
                P("NG-CG", "Nước ngọt có ga", "Đồ uống", "Bọt Xanh", 10_000, 7_000, 7, Demand.Steady,
                    Grid(("Quy cách", ["Lon 330ml/LON", "Chai 1,5L/C15"]))),
                P("ND-HOP", "Nước dừa đóng hộp", "Đồ uống", "Vườn Xanh", 18_000, 12_000, 4, Demand.New, Single()),
                P("NM-CA", "Nước mắm cá cơm", "Gia vị", "Nhà Làm", 45_000, 31_000, 3, Demand.Steady,
                    Grid(("Dung tích", ["500ml/500", "1L/1L"]))),
                P("NT-500", "Nước tương 500ml", "Gia vị", "Nhà Làm", 22_000, 15_000, 2, Demand.Steady, Single()),
                P("DA-DN", "Dầu ăn đậu nành", "Gia vị", "Sạch Việt", 52_000, 41_000, 3, Demand.Steady,
                    Grid(("Dung tích", ["1L/1L", "2L/2L"]))),
                P("MU-IOT", "Muối i-ốt 500g", "Gia vị", "Sạch Việt", 6_000, 3_800, 2, Demand.Steady, Single()),
                P("DU-1KG", "Đường trắng 1kg", "Gia vị", "Sạch Việt", 28_000, 22_000, 2.5, Demand.Steady, Single()),
                P("HN-GA", "Hạt nêm vị gà", "Gia vị", "Nhà Làm", 35_000, 26_000, 2, Demand.Steady,
                    Grid(("Khối lượng", ["400g/400", "900g/900"]))),
                P("TO-CHAI", "Tương ớt chai", "Gia vị", "Nhà Làm", 16_000, 10_500, 2, Demand.Declining, Single(), lowStock: true),
                P("MT-CC", "Mì tôm chua cay", "Mì, bún, phở khô", "Bếp Nhà", 4_500, 3_200, 20, Demand.Steady,
                    Grid(("Quy cách", ["Gói/GOI", "Thùng 30 gói/T30"]))),
                P("PK-500", "Phở khô 500g", "Mì, bún, phở khô", "Bếp Nhà", 25_000, 17_000, 1.5, Demand.Steady, Single()),
                P("BK-500", "Bún khô 500g", "Mì, bún, phở khô", "Bếp Nhà", 22_000, 15_000, 1.5, Demand.Steady, Single()),
                P("MD-200", "Miến dong 200g", "Mì, bún, phở khô", "Bếp Nhà", 18_000, 12_000, 0.8, Demand.Declining, Single()),
                P("GT-5KG", "Gạo thơm 5kg", "Mì, bún, phở khô", "Đồng Xanh", 125_000, 98_000, 2, Demand.Steady, Single()),
                P("BQ-BO", "Bánh quy bơ", "Bánh kẹo", "Lò Vàng", 32_000, 21_000, 3, Demand.Growing, Single()),
                P("KD-BT", "Kẹo dừa Bến Tre", "Bánh kẹo", "Lò Vàng", 25_000, 16_000, 1.5, Demand.Steady, Single()),
                P("SN-KT", "Snack khoai tây", "Bánh kẹo", "Giòn Tan", 12_000, 7_500, 6, Demand.Steady,
                    Grid(("Vị", ["Tự nhiên/TN", "Cay/CAY"]))),
                P("SC-TH", "Socola thanh", "Bánh kẹo", "Lò Vàng", 20_000, 13_000, 2, Demand.Steady, Single()),
                P("HD-RM", "Hạt điều rang muối", "Bánh kẹo", "Giòn Tan", 145_000, 105_000, 0, Demand.None, Single()),
                P("SU-CH", "Sữa chua", "Đồ uống", "Đồng Cỏ", 7_000, 4_800, 9, Demand.Steady,
                    Grid(("Vị", ["Có đường/CD", "Không đường/KD"]))),
                P("RC-CHEN", "Nước rửa chén", "Hóa phẩm", "Sạch Việt", 32_000, 23_000, 2, Demand.Steady,
                    Grid(("Dung tích", ["750ml/750", "1,5L/15L"]))),
                P("BG-GIAT", "Bột giặt", "Hóa phẩm", "Sạch Việt", 55_000, 41_000, 2, Demand.Steady,
                    Grid(("Khối lượng", ["1kg/1KG", "3kg/3KG"]))),
                P("KDR-180", "Kem đánh răng 180g", "Hóa phẩm", "Răng Xinh", 30_000, 20_000, 1.5, Demand.Steady, Single()),
                P("DG-650", "Dầu gội 650ml", "Hóa phẩm", "Tóc Mượt", 120_000, 85_000, 1, Demand.Steady, Single(), lowStock: true),
                P("GVS-10", "Giấy vệ sinh 10 cuộn", "Hóa phẩm", "Mềm Mại", 75_000, 55_000, 2, Demand.Steady, Single()),
                P("KG-RUT", "Khăn giấy rút", "Hóa phẩm", "Mềm Mại", 22_000, 15_000, 2.5, Demand.Growing, Single()),
                P("TG-10", "Trứng gà 10 quả", "Thực phẩm tươi", "Trại Nắng", 35_000, 27_000, 5, Demand.Steady, Single()),
                P("BM-SW", "Bánh mì sandwich", "Thực phẩm tươi", "Lò Vàng", 24_000, 16_000, 3, Demand.Steady, Single()),
                P("CF-HT", "Cà phê hòa tan hộp 20 gói", "Đồ uống", "Phố Núi", 58_000, 42_000, 1.5, Demand.Steady, Single()),
                P("TR-TL", "Trà túi lọc 25 túi", "Đồ uống", "Lá Biếc", 30_000, 19_000, 1, Demand.Declining, Single()),
            ]),
    ];

    private static ProductSeed P(
        string code, string name, string category, string? brand, decimal retailPrice, decimal unitCost, double dailyDemand,
        Demand demand, IReadOnlyList<Variant> variants, bool lowStock = false) =>
        new(code, name, category, brand, retailPrice, unitCost, dailyDemand, demand, variants, lowStock);

    private static IReadOnlyList<Variant> Single() => [new Variant("", new Dictionary<string, string>())];

    // Tích Descartes của các trục biến thể. Mỗi giá trị viết "Tên hiển thị/MÃ"; mã ghép vào mã SKU.
    private static IReadOnlyList<Variant> Grid(params (string Axis, string[] Values)[] axes)
    {
        IEnumerable<Variant> variants = [new Variant("", new Dictionary<string, string>())];
        foreach (var (axis, values) in axes)
        {
            variants = variants.SelectMany(variant => values.Select(value =>
            {
                var (label, code) = (value.Split('/')[0], value.Split('/')[1]);
                var attributes = new Dictionary<string, string>(variant.Attributes) { [axis] = label };
                return new Variant(variant.Suffix.Length == 0 ? code : $"{variant.Suffix}-{code}", attributes);
            }));
        }

        return variants.ToList();
    }
}
