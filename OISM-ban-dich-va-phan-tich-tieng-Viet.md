# OISM — Bản dịch tiếng Việt và phân tích yêu cầu

Tài liệu nguồn: Bản đề đầy đủ “Omnichannel Inventory and Sales Management System” bạn gửi trực tiếp trong cuộc trò chuyện, bổ sung cho tệp `Pasted text.txt` ban đầu.

**Phạm vi bản cập nhật:** Bản dịch đã bao gồm đầy đủ các mục a)–g) của phần 3.2 và mục 4 theo nội dung mới bạn cung cấp. Phần I dịch nội dung gốc và giữ các mã yêu cầu. Phần II phân tích yêu cầu, phân biệt các nội dung đề bài đã xác định với những đề xuất hoặc quyết định còn cần làm rõ.

## Phần I. Bản dịch đầy đủ

### Tên đề tài

- **Tên tiếng Anh:** Omnichannel Inventory and Sales Management System.
- **3.1.2. Tên tiếng Việt:** Hệ thống quản lý bán hàng và tồn kho đa kênh.
- **Tên viết tắt sử dụng trong tài liệu:** OISM.

### (*) 3.2. Nội dung đề xuất chính, bao gồm kết quả và sản phẩm

#### a) Bối cảnh

Trong môi trường bán lẻ hiện đại, doanh nghiệp ngày càng kinh doanh trên nhiều kênh bán hàng, bao gồm các cửa hàng bán hàng trực tiếp qua hệ thống POS và các nền tảng thương mại điện tử như Shopee, TikTok Shop, Lazada. Tuy nhiên, việc quản lý tồn kho và doanh số trên các kênh rời rạc đặt ra nhiều thách thức nghiêm trọng.

Các vấn đề phổ biến gồm bán vượt tồn kho — bán những mặt hàng đã hết do việc đồng bộ tồn kho bị chậm — theo dõi giá vốn hàng bán (COGS) không chính xác, và sai lệch dữ liệu do cập nhật tồn kho thủ công.

Để giải quyết các vấn đề này, doanh nghiệp bán lẻ cần một giải pháp phần mềm tích hợp, có khả năng xử lý nhiều thao tác đồng thời, theo dõi sổ giao dịch tồn kho theo thời gian thực, tự động tính giá vốn và ngăn chặn nghiêm ngặt việc bán vượt tồn kho trong kiến trúc SaaS phục vụ nhiều đơn vị thuê dùng.

#### b) Giải pháp đề xuất

Dự án đề xuất xây dựng Hệ thống quản lý bán hàng và tồn kho đa kênh (OISM) theo Clean Architecture và các nguyên tắc SaaS đa đơn vị thuê dùng (multi-tenant).

Cơ chế cốt lõi thay thế việc cập nhật trực tiếp tồn kho bằng một **sổ giao dịch tồn kho chỉ cho phép thêm mới** (append-only Inventory Ledger), bảo đảm mọi biến động đều có thể được kiểm tra và truy vết.

Hệ thống bao gồm:

- Bộ phận tiếp nhận đơn hàng tập trung (Order Ingestion Hub), vận hành bằng máy trạng thái.
- Bộ tính giá vốn bình quân gia quyền tự động.
- Mô-đun chống bán vượt tồn kho dựa trên cơ chế khóa.
- Giao diện bán hàng tại quầy dưới dạng ứng dụng web tiến bộ (PWA POS).

#### c) Yêu cầu chức năng

##### FR-AUTH — Quản lý đơn vị thuê dùng và xác thực

- **FR-AUTH-01 — Đăng ký đơn vị thuê dùng:** Cho phép tạo các đơn vị cửa hàng mới sử dụng hệ thống, mỗi đơn vị có không gian dữ liệu riêng biệt và được cấp một `TenantId` duy nhất.
- **FR-AUTH-02 — Xác thực người dùng:** Cung cấp chức năng đăng nhập/đăng xuất bằng email hoặc số điện thoại kết hợp mật khẩu; trả về JWT chứa `TenantId`, `UserId` và `Role`.
- **FR-AUTH-03 — Phân quyền theo vai trò (RBAC):** Thiết lập và kiểm soát quyền truy cập API/màn hình theo các vai trò: **Owner** — chủ sở hữu, có toàn quyền; **Staff** — nhân viên quản lý kho/đơn hàng; **Cashier** — thu ngân, chỉ sử dụng POS.
- **FR-AUTH-04 — Quản lý chi nhánh:** Cho phép chủ sở hữu tạo, cập nhật hoặc bật/tắt trạng thái hiển thị của các chi nhánh cửa hàng và kho thuộc quyền sở hữu.

##### FR-PROD — Quản lý sản phẩm và biến thể

- **FR-PROD-01 — Quản lý danh mục và thương hiệu:** Quản lý danh mục sản phẩm theo cấu trúc phân cấp và danh sách thương hiệu.
- **FR-PROD-02 — Quản lý sản phẩm và biến thể/SKU:** Hỗ trợ sản phẩm đơn và sản phẩm có biến thể như màu sắc, kích cỡ. Mỗi biến thể được gán một mã SKU duy nhất trong phạm vi cùng một tenant.
- **FR-PROD-03 — Quản lý mã vạch:** Tự động tạo hoặc nhập thủ công mã vạch EAN-13/Code128 cho từng SKU, phục vụ quét mã tại POS và kiểm kê.
- **FR-PROD-04 — Quản lý giá niêm yết:** Thiết lập giá bán lẻ và giá bán sỉ cho từng SKU; các giá này được phân biệt với giá vốn hàng bán.

##### FR-INV — Sổ giao dịch tồn kho và quản lý tồn kho

- **FR-INV-01 — Ghi nhận sổ giao dịch tồn kho:** Bắt buộc ghi nhận mọi biến động tồn kho — nhập, xuất, bán, trả hàng, kiểm kê, chuyển kho — vào sổ `InventoryTransaction` chỉ cho phép thêm mới. Mỗi bản ghi gồm `TenantId`, `BranchId`, `SKUId`, `Type` (IN/OUT), `Quantity`, `BalanceAfter`, `ReferenceId` và `CreatedAt`. Không được cập nhật trực tiếp các cột số lượng tồn thực tế.
- **FR-INV-02 — Quản lý phiếu nhập hàng:** Xử lý nhập hàng từ nhà cung cấp → tăng tồn thực tế (`on_hand`) → cập nhật giá vốn bình quân gia quyền.
- **FR-INV-03 — Quản lý chuyển kho:** Chuyển hàng từ chi nhánh A sang chi nhánh B theo quy trình hai bước: **xuất hàng vào trạng thái đang vận chuyển → xác nhận nhập tại nơi nhận**.
- **FR-INV-04 — Quản lý kiểm kê:** Tạo các phiên kiểm kê, ghi nhận số lượng đếm thực tế, so sánh với số dư trên phần mềm, sau đó tạo các bút toán điều chỉnh trong sổ giao dịch để cân bằng tồn kho.

##### FR-COST — Tính giá vốn bình quân gia quyền

- **FR-COST-01 — Tự động tính lại giá vốn theo đợt nhập:** Tự động tính lại đơn giá vốn của SKU khi xác nhận phiếu nhập hàng, theo công thức:

  `Giá vốn mới = [(Tồn cũ × Giá vốn cũ) + (Số lượng nhập × Đơn giá nhập)] / (Tồn cũ + Số lượng nhập)`

- **FR-COST-02 — Chốt giá vốn khi xuất hàng:** Lưu cố định đơn giá vốn bình quân gia quyền tại đúng thời điểm vào từng dòng đơn hàng (`OrderItem.CostPrice`) khi đơn chuyển sang `Confirmed` hoặc khi hoàn tất thanh toán tại POS.

##### FR-ORD — Trung tâm quản lý đơn hàng tập trung

- **FR-ORD-01 — Tiếp nhận đơn hàng đa kênh:** Chuẩn hóa đơn từ POS, đơn được tạo thủ công trên trang quản trị và đơn từ bộ mô phỏng webhook Shopee/TikTok về một mô hình đơn hàng chuẩn dùng chung (Canonical Order Model).
- **FR-ORD-02 — Máy trạng thái đơn hàng:** Thực thi nghiêm ngặt các bước chuyển trạng thái trong vòng đời đơn hàng: `Draft → Reserved → Confirmed → Completed`, hoặc `Cancelled`.
- **FR-ORD-03 — Xử lý đơn hàng:** Cho phép nhân viên xem xét đơn chờ xử lý, duyệt đơn, in phiếu giao hàng hoặc hủy đơn.

##### FR-RSE — Cơ chế chống bán vượt tồn kho

- **FR-RSE-01 — Tính tồn khả dụng:** Duy trì động số lượng tồn khả dụng của từng SKU tại từng chi nhánh:

  `available = on_hand - reserved`

  Trong đó, `available` là tồn có thể tiếp tục bán, `on_hand` là tồn thực tế, và `reserved` là số lượng đã giữ cho các đơn hàng.

- **FR-RSE-02 — Thực hiện giữ hàng:** Tăng số lượng giữ hàng khi đơn trực tuyến được tiếp nhận vào trung tâm ở trạng thái `Reserved`. Từ chối đơn nếu tồn khả dụng nhỏ hơn số lượng đặt mua.
- **FR-RSE-03 — Thực hiện trừ hàng/giải phóng hàng giữ:** Khi đơn chuyển sang `Confirmed`, giảm `on_hand`, giảm `reserved` và ghi giao dịch vào sổ tồn kho. Khi đơn chuyển sang `Cancelled`, giảm `reserved` để trả số lượng đã giữ về tồn khả dụng.

##### FR-POS — Bán hàng tại quầy bằng PWA POS

- **FR-POS-01 — Giao diện bán hàng nhanh:** Giao diện tối ưu cho thao tác cảm ứng và phím tắt, hỗ trợ tìm sản phẩm nhanh bằng tên, SKU hoặc mã vạch.
- **FR-POS-02 — Ràng buộc bán hàng tại quầy:** Kiểm tra tồn khả dụng ngay khi chọn sản phẩm; chặn việc bán vượt tồn khả dụng để bảo vệ lượng hàng đã giữ cho các đơn trực tuyến.
- **FR-POS-03 — Thanh toán và in hóa đơn:** Hỗ trợ tiền mặt, chuyển khoản qua mã QR, và tự động mở hộp thoại in của trình duyệt thông qua Browser Print API.
- **FR-POS-04 — Quy trình thanh toán nhanh:** Khi thanh toán tại POS, thực hiện **giữ hàng → xác nhận → trừ hàng** trong một giao dịch cơ sở dữ liệu nguyên tử duy nhất.

##### FR-REP — Báo cáo và cảnh báo

- **FR-REP-01 — Báo cáo doanh thu và lợi nhuận gộp:** Tổng hợp doanh thu thuần, tổng giá vốn hàng bán và lợi nhuận gộp theo công thức `Lợi nhuận gộp = Doanh thu - Giá vốn hàng bán`. Cho phép lọc theo khoảng thời gian, chi nhánh, kênh bán và SKU.
- **FR-REP-02 — Báo cáo tồn kho và tốc độ bán:** Theo dõi giá trị tồn kho theo giá vốn, các mặt hàng bán chạy và các mặt hàng bán chậm.
- **FR-REP-03 — Cảnh báo tồn kho:** Tự động hiển thị thông báo trên bảng điều khiển khi `available ≤ Threshold`, trong đó `Threshold` là ngưỡng tồn tối thiểu cần đặt hàng bổ sung của từng SKU.

##### FR-SIM — Mô phỏng webhook và xử lý thời gian thực

- **FR-SIM-01 — Bộ mô phỏng webhook thương mại điện tử:** Cung cấp công cụ cho lập trình viên/kiểm thử viên gửi dữ liệu giả lập từ Shopee/TikTok/Lazada, nhằm kiểm thử năng lực xử lý và các cơ chế khóa khi giữ hàng.
- **FR-SIM-02 — Thông báo đẩy theo thời gian thực:** Sử dụng SignalR để đẩy âm thanh/thông báo bật lên về đơn hàng mới đến trang quản trị/POS mà không cần tải lại trang.
- **FR-SIM-03 — Xử lý nền:** Sử dụng Hangfire cho các tác vụ bất đồng bộ nặng, bao gồm tự động hủy các đơn đã giữ hàng nhưng hết hạn và tổng hợp báo cáo hằng ngày.

#### d) Yêu cầu phi chức năng

##### NFR-PERF — Hiệu năng và xử lý đồng thời

- **NFR-PERF-01 — Thời gian phản hồi API:** Độ trễ p95 của việc tìm SKU/quét mã vạch tại POS phải dưới 200 ms. Việc tạo đơn/giao dịch ghi dữ liệu phải dưới 500 ms. Báo cáo doanh thu tổng hợp phải dưới 2 giây đối với tập dữ liệu dưới 100.000 bản ghi.
- **NFR-PERF-02 — Xử lý đồng thời và chống bán vượt tồn:** Xử lý được tình huống Flash Sale, ví dụ 50 đơn trực tuyến đồng thời cùng đặt mua một SKU chỉ còn một đơn vị khả dụng. Phải áp dụng khóa bi quan ở mức cơ sở dữ liệu (`SELECT ... FOR UPDATE`) hoặc khóa lạc quan bằng kiểm tra phiên bản trong quá trình cập nhật `reserved`/`on_hand`, nhằm tuyệt đối ngăn tồn khả dụng âm.

##### NFR-TENANT — Đa đơn vị thuê dùng và cách ly dữ liệu

- **NFR-TENANT-01 — Cách ly dữ liệu:** Sử dụng mô hình “cơ sở dữ liệu dùng chung, schema riêng/Tenant ID”. Tất cả bảng dữ liệu nghiệp vụ phải có `TenantId`. Bắt buộc áp dụng EF Core Global Query Filters để bảo đảm không rò rỉ dữ liệu giữa các tenant.
- **NFR-TENANT-02 — Khả năng mở rộng:** Tối ưu chỉ mục theo `(TenantId, CreatedAt)` và `(TenantId, SKUId)` cho các bảng sổ tồn kho và đơn hàng, nhằm duy trì hiệu năng truy vấn khi sổ giao dịch có hàng triệu bản ghi.

##### NFR-SEC — Bảo mật và tính toàn vẹn dữ liệu

- **NFR-SEC-01 — Mã hóa và xác thực:** Mật khẩu được băm bằng BCrypt/Argon2. Giao tiếp API được bảo vệ bằng HTTPS/TLS 1.3. JWT sử dụng access token có thời hạn 60 phút và refresh token được bảo vệ an toàn.
- **NFR-SEC-02 — Tuân thủ ACID:** Chuỗi **tạo đơn → giữ hàng → trừ hàng** bắt buộc thực hiện trong một giao dịch cơ sở dữ liệu nguyên tử, hoàn tác toàn bộ nếu có lỗi.
- **NFR-SEC-03 — Khả năng kiểm toán của sổ chỉ thêm mới:** Bảng `InventoryLedger` chỉ cho phép thêm mới, tuyệt đối không được `UPDATE` hoặc `DELETE`. Mọi sửa sai phải thực hiện bằng các giao dịch bù trừ trong sổ.

##### NFR-USA — Tính sẵn sàng và khả năng sử dụng

- **NFR-USA-01 — Tính sẵn sàng:** Mục tiêu đạt 99,5% thời gian hoạt động trong khung giờ kinh doanh từ 07:00 đến 22:00.
- **NFR-USA-02 — Khả năng sử dụng POS:** Giao diện POS tối giản, cho phép hoàn tất thanh toán với ít hơn 3 lần nhấp chuột hoặc bằng thao tác quét mã vạch rồi nhấn Enter. Bố cục thích ứng với máy tính để bàn, máy tính bảng và thiết bị POS cầm tay di động.

##### NFR-MAINT — Khả năng bảo trì và kiểm thử

- **NFR-MAINT-01 — Clean Architecture:** Phân tách rõ trách nhiệm giữa Domain — nghiệp vụ cốt lõi như giá vốn, giữ hàng; Application — các ca sử dụng; Infrastructure — EF Core, PostgreSQL, SignalR; và Presentation — Web API.
- **NFR-MAINT-02 — Độ bao phủ kiểm thử tự động:** Các mô-đun cốt lõi gồm sổ giao dịch tồn kho, giữ hàng chống bán vượt tồn và giá vốn phải duy trì độ bao phủ kiểm thử đơn vị và kiểm thử tích hợp tối thiểu 80%.

#### e) Cơ sở lý thuyết và thực hành

Sinh viên áp dụng quy trình phát triển Agile và UML 2.0 để mô hình hóa hệ thống.

Các tài liệu và thành phần cần bàn giao gồm:

1. Đặc tả yêu cầu người dùng (URS) và ma trận truy vết yêu cầu (RTM).
2. Tài liệu thiết kế kiến trúc: Clean Architecture và SaaS đa đơn vị thuê dùng.
3. Thiết kế chi tiết: sơ đồ lớp UML, sơ đồ quan hệ thực thể (ERD), sơ đồ tuần tự cho quy trình giữ hàng và thanh toán nhanh.
4. Kế hoạch triển khai phần mềm và đưa hệ thống vào vận hành.
5. Tài liệu kiểm thử: các ca kiểm thử đơn vị, kiểm thử tích hợp và kiểm thử đồng thời.
6. Tài liệu cài đặt và hướng dẫn sử dụng.
7. Kho mã nguồn và các thành phần có thể triển khai thông qua quy trình CI/CD: Dockerfile cho backend và frontend, `docker-compose.yml`, GitHub Actions để tự động build và chạy kiểm thử.

#### f) Sản phẩm dự kiến bàn giao

**Phần mềm hoàn chỉnh:**

- **Hệ thống backend:** .NET 8 Web API xử lý xác thực đa đơn vị thuê dùng, sổ giao dịch tồn kho, logic chống bán vượt tồn và xử lý đơn hàng theo thời gian thực.
- **Trang quản trị (Admin Dashboard):** Ứng dụng web để chủ cửa hàng quản lý sản phẩm, xem sổ giao dịch tồn kho, xử lý đơn hàng đa kênh và xem báo cáo lợi nhuận gộp.
- **Ứng dụng POS:** PWA hoạt động nhanh, thuận tiện cho thao tác cảm ứng của thu ngân, hỗ trợ quét mã vạch và thanh toán tức thời.
- **Tài liệu đi kèm:** Bộ tài liệu thiết kế kỹ thuật phần mềm đầy đủ, đặc tả API bằng Swagger/Postman và hướng dẫn triển khai.
- **Gói cài đặt demo:** Gói Docker Compose bao gồm Backend, Web Admin, POS App, cơ sở dữ liệu PostgreSQL và các script tạo dữ liệu mẫu (seed data), phục vụ triển khai nhanh.

#### g) Các gói công việc đề xuất

##### Gói công việc 1 — Thiết lập nền tảng cốt lõi và hạ tầng

- Thiết lập cấu trúc dự án theo Clean Architecture dưới dạng solution .NET 8.
- Xây dựng nền tảng kiến trúc đa đơn vị thuê dùng, thiết kế schema cơ sở dữ liệu PostgreSQL, thiết lập EF Core Global Query Filters và mô-đun xác thực JWT/phân quyền RBAC (FR-AUTH).

##### Gói công việc 2 — Danh mục sản phẩm và sổ giao dịch tồn kho chỉ thêm mới

- Phát triển các API quản lý sản phẩm, biến thể (SKU), mã vạch và giá bán (FR-PROD).
- Triển khai cơ chế sổ giao dịch tồn kho chỉ thêm mới (FR-INV), các quy trình nhập/chuyển kho và bộ tính giá vốn bình quân gia quyền tự động (FR-COST).

##### Gói công việc 3 — Trung tâm đơn hàng tập trung và bộ xử lý chống bán vượt tồn

- Triển khai mô hình đơn hàng chuẩn dùng chung và các quy tắc chuyển trạng thái của máy trạng thái (FR-ORD).
- Xây dựng bộ xử lý giữ/trừ hàng chống bán vượt tồn, sử dụng khóa cơ sở dữ liệu để xử lý đồng thời (FR-RSE).
- Phát triển bộ mô phỏng webhook thương mại điện tử và các tác vụ nền Hangfire (FR-SIM).

##### Gói công việc 4 — Giao diện POS và tích hợp thời gian thực

- Xây dựng trang quản trị bằng ReactJS để quản lý tồn kho, danh mục và đơn hàng.
- Xây dựng ứng dụng PWA POS bằng ReactJS, hỗ trợ quét mã vạch nhanh, in qua trình duyệt và thanh toán với các thao tác dữ liệu thực hiện trong một giao dịch nguyên tử (FR-POS).
- Tích hợp SignalR để thông báo đơn hàng và cảnh báo trên bảng điều khiển theo thời gian thực (FR-SIM-02).

##### Gói công việc 5 — Báo cáo, tích hợp AI, kiểm thử và triển khai

- Xây dựng chức năng phân tích, báo cáo doanh thu, giá vốn hàng bán và lợi nhuận gộp (FR-REP).
- Tích hợp dự báo bằng AI để đề xuất nhập hàng bổ sung.
- Viết kiểm thử đơn vị/kiểm thử tích hợp tự động, thực hiện kiểm thử đồng thời trong tình huống Flash Sale, thiết lập CI/CD bằng GitHub Actions, đóng gói bằng Docker và hoàn thiện tài liệu dự án.

### 4. Các ý kiến khác — đề xuất các nội dung liên quan, nếu có

Bản đề được cung cấp có tiêu đề mục này nhưng chưa ghi nội dung ý kiến hoặc đề xuất cụ thể.

## Phần II. Phân tích nội dung và các vấn đề cần làm rõ

### 1. Đề tài thực sự yêu cầu xây dựng điều gì?

Đây là một hệ thống bán lẻ đa kênh có nhiều doanh nghiệp cùng sử dụng phần mềm, mỗi doanh nghiệp có dữ liệu riêng và có thể có nhiều chi nhánh/kho. Hệ thống phải thống nhất các đơn hàng đến từ nhiều nguồn và kiểm soát chúng trên cùng dữ liệu tồn kho.

Ba trọng tâm nghiệp vụ là:

1. **Đúng số lượng:** Mọi nhập, xuất và điều chỉnh đều có lịch sử; các đơn đồng thời không được dùng chung một lượng hàng vượt mức cho phép.
2. **Đúng giá vốn:** Giá vốn cập nhật theo từng đợt nhập và được chốt trên dòng đơn hàng tại thời điểm quy định, để báo cáo quá khứ không thay đổi theo giá vốn hiện tại.
3. **Đúng phạm vi dữ liệu:** Người dùng chỉ thao tác trên dữ liệu tenant và các quyền được cấp.

Về kỹ thuật, mức độ khó tập trung ở giao dịch cơ sở dữ liệu, xử lý đồng thời, trạng thái đơn hàng và cách ly dữ liệu. Bản đầy đủ còn nêu việc tích hợp AI dự báo để đề xuất nhập hàng bổ sung trong gói công việc 5. Về đồ án, sản phẩm phải có backend, trang quản trị, POS, tài liệu thiết kế/API, bằng chứng kiểm thử và gói triển khai demo; chỉ xây dựng giao diện CRUD chưa đáp ứng nội dung đề xuất.

### 2. Cấu trúc và phạm vi yêu cầu

Tài liệu có **30 yêu cầu chức năng được đánh mã thuộc 9 nhóm** và **11 yêu cầu phi chức năng được đánh mã thuộc 5 nhóm**. Ngoài các mã này, gói công việc 5 còn yêu cầu tích hợp AI dự báo nhập hàng; nội dung AI chưa được bổ sung thành một FR/NFR riêng. Vì vậy, 30 FR không bao quát hết các chức năng được nêu ở mọi phần của đề.

| Nhóm chức năng | Số yêu cầu | Mục đích chính |
|---|---:|---|
| AUTH | 4 | Tenant, đăng nhập, phân quyền, chi nhánh/kho |
| PROD | 4 | Danh mục, thương hiệu, SKU, mã vạch, giá bán |
| INV | 4 | Sổ tồn kho, nhập hàng, chuyển kho, kiểm kê |
| COST | 2 | Giá vốn bình quân và giá vốn đã chốt trên đơn |
| ORD | 3 | Tiếp nhận và xử lý đơn theo trạng thái |
| RSE | 3 | Tính khả dụng, giữ hàng, trừ/giải phóng hàng |
| POS | 4 | Bán tại quầy, thanh toán, in biên nhận |
| REP | 3 | Doanh thu, lợi nhuận gộp, tồn kho và cảnh báo |
| SIM | 3 | Dữ liệu giả lập, thông báo trực tiếp và tác vụ nền |

Những ranh giới cần hiểu đúng:

- **Frontend đã được xác định là ReactJS.** Gói công việc 4 yêu cầu ReactJS cho cả Admin Dashboard và PWA POS. Đề chưa quy định hai giao diện dùng chung một dự án frontend hay tách thành hai dự án, nhưng gói demo phải có đủ Web Admin và POS App.
- **AI dự báo nhập hàng đã nằm trong nội dung công việc.** Đây không còn là tính năng tự đề xuất thêm; tuy nhiên, đề mới nêu mục tiêu, chưa xác định mô hình, dữ liệu đầu vào, đầu ra và tiêu chí nghiệm thu. Không có yêu cầu tự động đặt mua hoặc tự động sửa tồn kho dựa trên dự báo.
- **Kết nối sàn đang được mô tả ở mức giả lập webhook.** Tài liệu chưa yêu cầu rõ việc kết nối API thật, cấp quyền tài khoản sàn, đẩy tồn kho trở lại sàn hoặc xử lý giới hạn API. FR-ORD-01 nêu Shopee/TikTok; FR-SIM-01 bổ sung Lazada. Cần thống nhất Lazada có nằm trong toàn bộ luồng chuẩn hóa đơn hay chỉ trong công cụ thử nghiệm.
- **Chống bán vượt tồn được mô tả trong hệ thống trung tâm.** Nếu sau này kết nối sàn thật, việc OISM từ chối giữ hàng khi hết tồn chưa đồng nghĩa sàn bên ngoài chưa từng nhận đơn vượt tồn. Cần bổ sung cơ chế đồng bộ và quy tắc xử lý đơn bị từ chối nếu muốn cam kết trên toàn bộ các kênh thực tế.
- **PWA chưa đồng nghĩa phải bán hàng ngoại tuyến.** Tài liệu không nêu chế độ offline. Nếu cần bán offline thì phải bổ sung chính sách phân bổ tồn và xử lý xung đột; đây là phần mở rộng đáng kể so với các luồng hiện có.
- **Thanh toán QR chưa đồng nghĩa tích hợp ngân hàng tự động.** Chưa rõ hệ thống chỉ hiển thị mã QR và thu ngân xác nhận, hay nhận kết quả thanh toán từ bên ngoài.
- **In qua trình duyệt chưa xác định hóa đơn điện tử.** Nội dung hiện tại mô tả biên nhận/hóa đơn bán hàng được in; chưa mô tả phát hành hóa đơn điện tử hoặc tích hợp thiết bị in chuyên dụng.
- **Lợi nhuận gộp khác lợi nhuận ròng.** Tài liệu mới yêu cầu doanh thu trừ giá vốn; chưa quy định việc trừ các chi phí như nhân sự, thuê mặt bằng hoặc chi phí vận hành.

### 3. Thuật ngữ, vai trò và phạm vi sở hữu

| Thuật ngữ | Cách hiểu trong đề tài |
|---|---|
| Tenant | Một đơn vị/doanh nghiệp sử dụng hệ thống với dữ liệu được cách ly |
| Branch | Chi nhánh thuộc một tenant; cần xác định quan hệ với kho |
| Product | Sản phẩm chung, ví dụ “Áo thun A” |
| SKU/Variant | Đơn vị hàng phân biệt để quản lý tồn, ví dụ “Áo thun A, đen, M” |
| on_hand | Tồn thực tế theo sổ hệ thống tại vị trí quản lý |
| reserved | Số lượng đã giữ cho các đơn chưa thực hiện trừ tồn |
| available | Số lượng còn có thể nhận bán, bằng on_hand trừ reserved |
| Inventory Ledger | Lịch sử bất biến của các biến động tồn kho |
| WAC | Đơn giá vốn bình quân gia quyền |
| COGS | Giá vốn hàng bán, thường tổng hợp từ số lượng bán và đơn giá vốn đã chốt |
| Canonical Order Model | Mô hình đơn hàng nội bộ dùng chung sau khi chuẩn hóa từng nguồn |
| Atomic transaction | Giao dịch hoặc thành công toàn bộ, hoặc hoàn tác toàn bộ |
| Idempotency | Xử lý lại cùng một yêu cầu/sự kiện mà không tạo thêm tác động nghiệp vụ |
| p95 | Phân vị 95 của thời gian phản hồi; mục tiêu p95 dưới 200 ms nghĩa là khoảng 95% lượt đo đạt dưới ngưỡng này |

Các vai trò được nêu rõ là Owner, Staff và Cashier. Owner có toàn quyền trong phạm vi tenant; không có mô tả về quản trị viên toàn nền tảng. Staff quản lý kho/đơn hàng, Cashier dùng POS. Quyền xem giá vốn, báo cáo lợi nhuận, hủy đơn, hoàn tiền và truy cập từng chi nhánh chưa được phân định chi tiết.

Tenant không phải chi nhánh: một tenant có thể có nhiều chi nhánh. Người dùng có thể thuộc nhiều tenant hoặc nhiều chi nhánh hay không cũng chưa được quy định. Nhân viên phát triển/kiểm thử là đối tượng dùng simulator; chưa có yêu cầu rằng họ là một vai trò nghiệp vụ của cửa hàng.

### 4. Cách các nghiệp vụ cốt lõi phối hợp

#### 4.1. Nhập hàng và tính giá vốn

Theo yêu cầu, xác nhận phiếu nhập làm tăng tồn và tính lại giá vốn. Ví dụ minh họa:

- Tồn cũ: 10 sản phẩm, giá vốn 100.000 đồng/sản phẩm.
- Nhập thêm: 5 sản phẩm, đơn giá nhập 130.000 đồng/sản phẩm.
- Giá vốn mới: `(10 × 100.000 + 5 × 130.000) / 15 = 110.000 đồng/sản phẩm`.
- Tồn sau nhập: 15 sản phẩm; giá trị tồn theo ví dụ: 1.650.000 đồng.

Đây là cách tính bình quân sau mỗi lần nhập được xác nhận. Dù tên FR-COST-01 sử dụng chữ “Batch”, nội dung không mô tả việc đợi cuối ngày/cuối tháng mới tính giá vốn.

**Đề xuất:** Việc xác nhận phiếu, ghi sổ, cập nhật số dư nếu có và cập nhật giá vốn cần thành công hoặc thất bại cùng nhau. Phải ngăn xác nhận cùng phiếu nhập hai lần. Cần xác định đơn giá nhập có bao gồm chiết khấu, thuế hoặc chi phí vận chuyển hay không, cùng độ chính xác và quy tắc làm tròn.

#### 4.2. Đơn trực tuyến: giữ hàng khác với trừ hàng

Giả sử một SKU tại một chi nhánh có `on_hand = 10`, `reserved = 3`, nên `available = 7`. Có một đơn mới mua 5 sản phẩm:

| Thời điểm | on_hand | reserved | available | Ý nghĩa |
|---|---:|---:|---:|---|
| Ban đầu | 10 | 3 | 7 | Đã có 3 sản phẩm dành cho các đơn khác |
| Giữ thêm 5 cho đơn mới | 10 | 8 | 2 | Chưa trừ tồn thực tế nhưng chỉ còn 2 sản phẩm có thể bán |
| Xác nhận đơn mới | 5 | 3 | 2 | Trừ 5 khỏi tồn và bỏ phần giữ 5 của đơn đó |

Nếu hủy đơn mới **khi vẫn đang Reserved**, kết quả từ hàng thứ hai là `on_hand = 10`, `reserved = 3`, `available = 7`.

Khi chuyển từ Reserved sang Confirmed, tồn khả dụng không tự giảm thêm lần nữa: lượng hàng đã bị loại khỏi khả dụng từ lúc giữ. Nếu vừa trừ `on_hand` vừa không giảm `reserved`, hệ thống sẽ giữ thừa hàng và tính sai tồn khả dụng.

Luồng này cần lưu được phần giữ hàng thuộc đơn nào, SKU nào, chi nhánh nào và số lượng bao nhiêu; chỉ có một tổng `reserved` sẽ gây khó khăn cho việc hủy, hết hạn và đối soát.

#### 4.3. Bán hàng POS

Kiểm tra khi chọn sản phẩm giúp thu ngân nhận phản hồi sớm, nhưng tồn có thể thay đổi trước khi bấm thanh toán. Vì vậy, **đề xuất bắt buộc kiểm tra lại tại giao dịch thanh toán ở backend**, cùng cơ chế kiểm soát đồng thời như đơn trực tuyến.

Trong một giao dịch POS, hệ thống cần kiểm tra/giữ lượng hàng hợp lệ, tạo hoặc cập nhật đơn, chốt giá vốn, trừ tồn và ghi sổ. Nếu bất kỳ thao tác ghi nào thất bại, giao dịch phải được hoàn tác. Thông báo và lệnh in nên dựa trên kết quả đã lưu thành công; in lỗi không nên tự làm phát sinh thêm một lần bán.

Tài liệu chưa quy định POS kết thúc ở Confirmed hay chuyển ngay sang Completed, và chưa nói rõ cách xử lý nếu thanh toán QR còn chờ xác nhận.

#### 4.4. Chốt giá vốn và báo cáo

Với giá vốn 110.000 đồng/sản phẩm, nếu bán 2 sản phẩm với giá 150.000 đồng/sản phẩm, giả sử không có thuế, giảm giá hoặc hàng trả:

- Doanh thu: 300.000 đồng.
- Giá vốn hàng bán: 220.000 đồng.
- Lợi nhuận gộp: 80.000 đồng.

Giả sử lô nhập sau làm giá vốn hiện tại tăng lên 120.000 đồng, báo cáo của đơn cũ vẫn phải sử dụng giá vốn 110.000 đồng đã lưu ở `OrderItem.CostPrice`. Nếu lấy giá vốn hiện tại để tính lại toàn bộ đơn cũ, lợi nhuận lịch sử sẽ bị thay đổi.

### 5. Các điểm mâu thuẫn hoặc chưa đủ rõ cần chốt trước khi lập trình

#### 5.1. Sổ chỉ thêm mới và cột on_hand

**Trong tài liệu:** FR-INV-01 cấm cập nhật trực tiếp cột tồn, nhưng FR-INV-02, FR-RSE-03 và NFR-PERF-02 lại nói đến tăng/giảm/cập nhật `on_hand`.

**Vấn đề:** Chưa rõ tồn hiện tại được tính hoàn toàn từ sổ hay được lưu thêm ở một bảng số dư. “Không cập nhật trực tiếp” có thể nghĩa là cấm mọi cập nhật vật lý, hoặc chỉ cấm sửa số dư ngoài nghiệp vụ ghi sổ. Hai cách hiểu dẫn đến thiết kế khác nhau.

**Đề xuất cần được thống nhất:** Chọn một mô hình rõ ràng. Nếu dùng bảng số dư để đọc nhanh, quy định sổ là nguồn lịch sử chuẩn; số dư chỉ được thay đổi cùng giao dịch ghi sổ và có thể đối soát/tái dựng. Khi chọn cách này, phải sửa câu cấm cập nhật trong đặc tả để cho phép cơ chế đó. Nếu cấm cập nhật số dư hoàn toàn, cần thiết kế cách tính tồn từ sổ cùng đối tượng được khóa khi giữ hàng. Không nên để hai nhóm phát triển tự hiểu theo hai cách khác nhau.

#### 5.2. Một transaction cho toàn bộ vòng đời đơn trực tuyến

**Trong tài liệu:** NFR-SEC-02 yêu cầu tạo đơn → giữ hàng → trừ hàng trong một transaction; trong khi đơn trực tuyến có thể nằm ở Reserved chờ nhân viên duyệt hoặc chờ hết hạn.

**Vấn đề:** Nếu trạng thái Reserved phải được lưu để xử lý sau, không thể coi toàn bộ vòng đời nhiều yêu cầu độc lập là một transaction cơ sở dữ liệu đang mở xuyên suốt.

**Đề xuất:** Làm rõ rằng POS có thể chạy toàn bộ chuỗi trong một transaction, còn đơn trực tuyến dùng các transaction nguyên tử cho từng bước: tạo đơn + giữ hàng; xác nhận + chốt giá vốn + trừ tồn + ghi sổ; hủy/hết hạn + giải phóng phần giữ. Chuyển trạng thái phải được kiểm tra và lưu cùng tác động nghiệp vụ tương ứng.

#### 5.3. Hủy đơn sau khi đã Confirmed

**Trong tài liệu:** Confirmed đã giảm `reserved` và `on_hand`, nhưng quy tắc Cancelled chỉ mô tả giảm `reserved`.

**Vấn đề:** Áp dụng quy tắc hủy này sau Confirmed có thể giảm giữ hàng hai lần và không xử lý được lượng tồn đã xuất.

**Đề xuất:** Lập bảng chuyển trạng thái theo trạng thái xuất phát. Hủy từ Reserved chỉ giải phóng phần giữ còn hiệu lực. Sau Confirmed, phải có quy trình riêng xét hàng đã giao/chưa giao, hoàn tiền và có nhận lại hàng hay không. Chỉ nhập lại tồn khi nghiệp vụ thực sự cho phép; nếu cần điều chỉnh sổ, thêm giao dịch bù trừ và liên kết với giao dịch gốc. Không tự đưa hàng đã giao trở lại tồn chỉ vì đơn bị đánh dấu hủy.

#### 5.4. Confirmed có nghĩa là duyệt đơn hay xuất kho?

**Trong tài liệu:** FR-COST-02 có tiêu đề chốt giá vốn khi xuất hàng nhưng lấy thời điểm Confirmed; FR-ORD-03 cũng có thao tác duyệt đơn.

**Vấn đề:** Nếu nhân viên chỉ duyệt nhưng hàng còn nằm trong kho, giảm một đại lượng được gọi là “tồn thực tế” ngay lúc đó có thể không phù hợp cách hiểu nghiệp vụ.

**Đề xuất:** Định nghĩa chính xác Confirmed và Completed. Có thể thống nhất Confirmed chính là thời điểm ghi nhận xuất kho, hoặc bổ sung trạng thái xuất/giao hàng và thay đổi thời điểm trừ tồn. Đây là quyết định nghiệp vụ, không nên chỉ chọn theo tên trạng thái.

#### 5.5. Giá vốn theo tenant hay theo chi nhánh?

Tồn kho được quản lý theo chi nhánh, nhưng công thức giá vốn chỉ nói “giá vốn SKU”. Chưa rõ `Old Stock` là tồn toàn tenant hay tồn của một chi nhánh. Chưa rõ có tính hàng đang giữ hoặc hàng đang vận chuyển vào mẫu số hay không.

**Đề xuất:** Quy định rõ phạm vi định giá. Nếu cùng chi nhánh giữ hàng nhưng chưa xuất thì việc giữ hàng chỉ làm thay đổi khả dụng, không tự làm giảm lượng tồn dùng để định giá theo cách hiểu hiện tại. Phải ghi rõ cách xử lý giá vốn của hàng chuyển chi nhánh, hàng trả lại và điều chỉnh kiểm kê; không tự suy ra mọi nghiệp vụ IN đều có cùng công thức nhập mua.

#### 5.6. Chuyển kho hai bước còn thiếu trạng thái trung gian

Tài liệu mới nói xuất khỏi A rồi nhập B. Cần làm rõ lượng hàng đang vận chuyển được theo dõi ở đâu, khi nào được bán tại B, có cho phép nhận một phần, thiếu/hỏng hàng hoặc hủy chuyển hay không.

**Đề xuất:** Theo dõi hàng đang vận chuyển riêng, liên kết hai đầu giao dịch bằng chứng từ chuyển kho và mang theo giá trị hàng chuyển. Không cộng vào khả dụng của B trước khi B xác nhận nhận hàng. Ở cấp doanh nghiệp, cần đối soát tổng hàng tại các chi nhánh cộng hàng đang vận chuyển, có xét các điều chỉnh được duyệt.

#### 5.7. Kiểm kê khi đang có hàng được giữ

Nếu `reserved = 8` nhưng kiểm kê chỉ thấy 6 sản phẩm, điều chỉnh `on_hand` về 6 sẽ làm `available = -2`, xung đột với yêu cầu không có tồn khả dụng âm.

**Đề xuất:** Quy định cách khóa/chốt phạm vi kiểm kê và xử lý thiếu hàng trước khi ghi nhận kết quả cuối cùng, chẳng hạn phân bổ lại hoặc hủy có kiểm soát một số phần giữ. Cần phản ánh đúng thiếu hụt thực tế và lưu dấu xử lý; không tự nâng số kiểm kê để che sai lệch.

#### 5.8. Trả hàng được nhắc đến nhưng chưa có quy trình

FR-INV-01 yêu cầu ghi nhận Return, nhưng không có chức năng riêng mô tả trả một phần/toàn bộ, hàng hỏng không nhập lại để bán, hoàn tiền, trả nhà cung cấp hoặc tác động lên doanh thu và giá vốn.

**Đề xuất:** Bổ sung ca sử dụng trả hàng và liên kết với dòng đơn gốc. Cần quyết định khi nào tăng tồn, đơn giá dùng cho giao dịch trả hàng và cách điều chỉnh báo cáo. Không mặc định giá vốn hiện tại luôn là giá phù hợp cho hàng trả của đơn cũ.

#### 5.9. Webhook trùng, đến sai thứ tự và thao tác gửi lại

Một đơn có thể được gửi lại do retry; thu ngân có thể nhấn thanh toán hai lần; tác vụ nền có thể chạy lại. Tài liệu chưa mô tả cách bảo đảm xử lý lặp không phát sinh đơn, giữ hàng hoặc xuất hàng trùng.

**Đề xuất:** Có định danh đơn ngoài theo tenant/kênh, định danh sự kiện để nhận diện webhook trùng và khóa idempotency cho lệnh thanh toán. Phân biệt “đơn đã tồn tại” với “sự kiện mới cập nhật một đơn đã tồn tại” để không bỏ mất cập nhật hợp lệ. Máy trạng thái cần xử lý sự kiện đến sai thứ tự, cùng ràng buộc ở cơ sở dữ liệu để chống trùng khi nhiều yêu cầu chạy đồng thời.

#### 5.10. Mô hình tenant đang có hai cách diễn đạt khác nhau

“Shared Database, Separate Schema/Tenant ID” chưa xác định dùng schema riêng cho từng tenant hay dùng chung schema và phân tách bằng `TenantId`. Việc yêu cầu mọi bảng có `TenantId` và dùng global filters gợi ý cách thứ hai, nhưng đây chỉ là suy luận từ tài liệu.

**Đề xuất:** Chốt một chiến lược trong tài liệu kiến trúc. Ngoài bộ lọc truy vấn, mọi thao tác ghi phải xác minh dữ liệu liên quan cùng tenant; JWT phải được xác thực; các truy vấn đặc biệt, tác vụ Hangfire, nhóm thông báo SignalR và khóa bộ nhớ đệm cũng phải giữ đúng phạm vi tenant. Kiểm tra bằng cách truy cập ID của tenant khác và thử tạo quan hệ giữa dữ liệu của hai tenant. Không xem một cơ chế lọc truy vấn là bằng chứng đầy đủ rằng mọi đường truy cập đều đã cách ly.

#### 5.11. Sổ hiện có chưa mô tả đủ thông tin kiểm toán

Các trường đã nêu là nền tảng, nhưng `Type = IN/OUT` không tự phân biệt được nhập mua, bán, trả hàng hay điều chỉnh. `ReferenceId` cũng chưa nói tham chiếu loại chứng từ nào. Tên `InventoryTransaction` và `InventoryLedger` đang được dùng ở hai đoạn khác nhau; cần thống nhất tên mô hình/bảng.

**Đề xuất:** Xác định cách lưu hoặc truy ra loại nghiệp vụ, chứng từ nguồn, người thực hiện, lý do điều chỉnh và giao dịch gốc bị bù trừ. Nếu cần kiểm toán giá trị tồn, xác định nơi lưu đơn giá/giá trị tại thời điểm giao dịch. `BalanceAfter` phải được tính theo thứ tự giao dịch nhất quán cho cùng tenant/chi nhánh/SKU; chỉ sắp theo dấu thời gian có thể không đủ để phân biệt các giao dịch đồng thời.

Tính bất biến cần được thực thi qua quyền/cơ chế cơ sở dữ liệu của ứng dụng và kiểm thử, không chỉ bằng việc không hiển thị nút sửa/xóa. Điều này không tự đồng nghĩa hệ thống chống được mọi thay đổi của quản trị viên cơ sở dữ liệu có toàn quyền.

#### 5.12. Doanh thu thuần và thời điểm ghi nhận chưa được định nghĩa

Chưa rõ báo cáo lấy đơn Confirmed hay Completed; cách trừ chiết khấu, hàng trả và hoàn tiền; có tính thuế, phí vận chuyển, phí sàn hay không. “Bán chạy”, “bán chậm” và ngưỡng tồn cũng thiếu khoảng thời gian/phạm vi đo cụ thể.

**Đề xuất:** Viết công thức và trạng thái dữ liệu nguồn cho từng chỉ tiêu. Thống nhất múi giờ báo cáo, cách chia ngày và cách xử lý đơn bị sửa trạng thái sau khi báo cáo ngày đã tổng hợp. Xác định ngưỡng tồn theo SKU dùng chung hay theo từng chi nhánh.

#### 5.13. Hết hạn giữ hàng và xác nhận đơn có thể xảy ra đồng thời

Hangfire có nhiệm vụ hủy đơn giữ hàng hết hạn, nhưng chưa nêu thời hạn, điều kiện gia hạn và trường hợp nhân viên xác nhận đúng lúc tác vụ hết hạn chạy.

**Đề xuất:** Lưu thời điểm hết hạn và dùng điều kiện cập nhật trạng thái cùng cơ chế kiểm soát đồng thời. Chỉ một kết quả cuối hợp lệ được phép thắng; không được vừa xuất hàng vừa giải phóng phần giữ lần nữa. Tác vụ chạy lại phải không gây thêm biến động.

#### 5.14. Thanh toán ngoài hệ thống không nằm trong transaction cơ sở dữ liệu

FR-POS-04 quy định thao tác dữ liệu nguyên tử khi thanh toán; tài liệu chưa phân biệt trạng thái thanh toán và trạng thái đơn.

**Đề xuất:** Nếu tiền mặt/QR được thu ngân xác nhận thủ công, nêu rõ đó là cơ chế hiện tại. Nếu dùng thanh toán tích hợp, quy định xử lý khi đã nhận tiền nhưng ghi đơn thất bại, hoặc ghi đơn xong nhưng xác nhận tiền chưa tới. Transaction cơ sở dữ liệu không tự hoàn tác giao dịch tiền ở hệ thống bên ngoài. Việc hủy đơn, hoàn tiền và nhập lại hàng cần các điều kiện riêng.

#### 5.15. AI dự báo nhập hàng có trong kế hoạch nhưng chưa có đặc tả

**Trong tài liệu:** Gói công việc 5 yêu cầu “Integrate AI forecasting for stock reorder recommendations”, nhưng mục c) chưa có mã chức năng tương ứng, mục d) chưa nêu tiêu chí đánh giá và mục f) chưa mô tả cách bàn giao riêng cho tính năng này. Không thể vì thiếu mã FR mà tự loại AI khỏi phạm vi công việc.

**Các điểm cần làm rõ:**

- **Đối tượng dự báo:** Dự báo nhu cầu của SKU theo tenant hay từng chi nhánh, cho khoảng thời gian nào và cập nhật bao lâu một lần?
- **Dữ liệu đầu vào:** Dùng lịch sử bán hàng theo trạng thái nào, xử lý trả hàng/đơn hủy ra sao, cần bao nhiêu dữ liệu và xử lý SKU mới thế nào? Cần xác định cách nhận biết giai đoạn hết hàng để không tự coi không có doanh số là không có nhu cầu.
- **Đầu ra và hành động:** Hiển thị nhu cầu dự kiến, số lượng nên nhập hay thời điểm cần nhập; ai xem và ai phê duyệt? Nếu tính số lượng nhập, cần chốt cách xét tồn khả dụng, hàng đang về, thời gian nhà cung cấp giao hàng và mức dự phòng. Các yếu tố này là đề xuất cần đặc tả, chưa phải trường dữ liệu bắt buộc trong bản gốc.
- **Phương pháp và nghiệm thu:** Thống nhất thế nào là đáp ứng yêu cầu AI, tập dữ liệu đánh giá, phương án cơ sở để so sánh, chỉ số sai số và ngưỡng chấp nhận. Không tự giả định đề yêu cầu chatbot, mô hình ngôn ngữ hoặc một dịch vụ AI cụ thể.
- **Vận hành và phân quyền:** Chốt nơi chạy dự báo, lịch chạy, thời điểm dữ liệu được cập nhật, phạm vi dữ liệu từng tenant và cách hiển thị khi thiếu dữ liệu hoặc dự báo thất bại. Nếu sử dụng dịch vụ bên ngoài, cần xác định cấu hình/phụ thuộc để gói demo chạy được.

**Đề xuất:** Bổ sung yêu cầu có mã và ca kiểm thử vào URS/RTM sau khi chốt phạm vi. Tách chức năng dự báo khỏi cảnh báo ngưỡng tĩnh `available ≤ Threshold` của FR-REP-03: cảnh báo dựa trên ngưỡng không tự chứng minh đã có AI forecasting. Đầu ra dự báo là khuyến nghị; bản đề chưa yêu cầu tự tạo phiếu mua hay tự ghi biến động vào ledger.

### 6. Đánh giá các yêu cầu kỹ thuật và phi chức năng

#### Hiệu năng

Các ngưỡng 200 ms, 500 ms và 2 giây là mục tiêu định lượng hữu ích, nhưng cần bổ sung điều kiện đo: phần cứng, dữ liệu mỗi tenant, số người dùng đồng thời, có tính mạng hay không, truy vấn đã làm nóng bộ nhớ đệm hay chưa, và đo trong thời gian bao lâu. Tài liệu chỉ nói rõ p95 cho tìm kiếm/quét mã; chưa gắn phân vị đo cho hai ngưỡng còn lại.

Trong trường hợp 50 yêu cầu cùng mua một đơn vị còn lại, cần ghi nhận cả tính đúng và độ trễ do chờ khóa. Nếu không có bổ sung tồn, hủy hoặc giải phóng hàng trong bài thử, chỉ một yêu cầu được giữ thành công; 49 yêu cầu còn lại phải thất bại có kiểm soát.

Các chỉ mục `(TenantId, CreatedAt)` và `(TenantId, SKUId)` là định hướng trong đề bài, chưa phải thiết kế chỉ mục hoàn chỉnh. Vì tồn được quản lý theo chi nhánh, cần xem xét đường truy vấn dùng cả `TenantId`, `BranchId`, `SKUId`. Nếu SKU nằm ở dòng đơn, chỉ mục SKU phải đặt ở bảng dòng đơn tương ứng, không mặc định bảng Orders có sẵn cột này.

#### Xử lý đồng thời

Kiểm tra `available` và ghi thay đổi phải nằm trong cùng cơ chế bảo vệ. Nếu hai yêu cầu cùng đọc thấy 1 sản phẩm rồi đều ghi giữ 1, kiểm tra ở giao diện hoặc kiểm tra tách rời giao dịch không ngăn được bán vượt tồn.

Khóa bi quan hoặc kiểm tra phiên bản đều cần được thiết kế cho đúng các hàng dữ liệu liên quan. Nếu dùng phiên bản, xung đột phải dẫn đến thất bại có kiểm soát hoặc đọc lại và tính lại, không được tiếp tục với dữ liệu cũ. Đơn nhiều SKU cần quy tắc giữ toàn bộ hoặc giữ một phần; nếu chọn toàn bộ, lỗi ở một SKU phải hoàn tác các SKU đã giữ trước đó. Cũng cần thứ tự xử lý nhất quán để hạn chế các transaction chờ nhau vòng tròn.

#### Bảo mật

Tài liệu đã nêu băm mật khẩu, HTTPS, JWT, RBAC và cách ly tenant. Các chi tiết còn thiếu gồm thời hạn refresh token, thu hồi/đổi refresh token, ý nghĩa đăng xuất đối với token còn hiệu lực, giới hạn thử đăng nhập và phạm vi quyền theo chi nhánh. Đây là các điểm cần bổ sung vào thiết kế; chưa có cơ sở để kết luận một cơ chế cụ thể đã được triển khai.

#### Tính sẵn sàng

Mục tiêu 99,5% cần có kỳ đo và định nghĩa sự cố. Ví dụ, nếu tính 30 ngày đều mở cửa 15 giờ/ngày thì thời gian thuộc phạm vi đo là 450 giờ; 0,5% tương ứng tối đa khoảng **2 giờ 15 phút** gián đoạn trong kỳ đó. Đây là minh họa theo giả định 30 ngày, không phải kỳ đo đã được tài liệu ấn định.

Cần xác định có loại trừ bảo trì hay không, cách giám sát, sao lưu và khôi phục. Docker/CI/CD hỗ trợ triển khai nhưng không tự chứng minh đạt tỷ lệ sẵn sàng.

#### Khả năng sử dụng

“Ít hơn 3 lần nhấp” phải xác định điểm bắt đầu/kết thúc và loại đơn dùng để đo, ví dụ một sản phẩm, thanh toán tiền mặt, không giảm giá. Cần làm rõ thao tác quét mã, nhập số lượng và chọn phương thức thanh toán được tính ra sao. Bố cục trên desktop, tablet và thiết bị cầm tay đều cần được kiểm tra thực tế.

#### Khả năng kiểm thử và bảo trì

Mục tiêu 80% cần làm rõ là độ bao phủ dòng lệnh hay nhánh, theo từng mô-đun hay gộp các mô-đun, và cách tính khi kết hợp unit/integration tests. Độ bao phủ cao không thay thế bằng chứng kiểm thử race condition, rollback, xử lý trùng và tenant isolation.

Clean Architecture nên được thể hiện bằng hướng phụ thuộc và việc đặt quy tắc nghiệp vụ đúng lớp. Tài liệu đã chọn .NET 8, EF Core, PostgreSQL, SignalR, Hangfire và ReactJS cho Admin Dashboard/PWA POS. Phần AI chưa chỉ định thư viện, mô hình hoặc dịch vụ; Docker Compose, GitHub Actions và tài liệu API Swagger/Postman cũng đã được nêu rõ trong sản phẩm/gói công việc. Bản phân tích này không kiểm tra phiên bản hay tình trạng hỗ trợ hiện tại của các công nghệ đó.

### 7. Mô hình dữ liệu có thể dùng để triển khai

Các nhóm thực thể sau là **gợi ý thiết kế**, không phải danh sách bảng đã được đề bài phê duyệt:

| Nhóm | Thực thể ứng viên | Điểm phải làm rõ |
|---|---|---|
| Đơn vị và quyền | Tenant, User, Role, UserBranch | Người dùng thuộc bao nhiêu tenant/chi nhánh; quyền theo chi nhánh |
| Địa điểm | Branch, Warehouse hoặc một mô hình Location thống nhất | Branch và Warehouse là hai cấp hay hai loại địa điểm |
| Hàng hóa | Category, Brand, Product, SKU, Barcode | SKU duy nhất trong tenant; quy tắc mã vạch; giá lẻ/sỉ |
| Nhập mua | Supplier, PurchaseReceipt, PurchaseReceiptItem | Danh mục nhà cung cấp; điều kiện xác nhận; giá nhập |
| Tồn kho | InventoryTransaction, InventoryBalance nếu được cho phép | Nguồn dữ liệu chuẩn; số dư; tính bất biến và đối soát |
| Giá vốn | Bản ghi chi phí hoặc trường giá vốn trên thực thể theo phạm vi đã chốt | Giá vốn theo tenant hay chi nhánh; lịch sử định giá |
| Giữ hàng | Reservation hoặc dữ liệu tương đương trên dòng đơn | Số lượng đang giữ, thời hạn, trạng thái đã sử dụng/giải phóng |
| Đơn hàng | Order, OrderItem, Payment | Kênh, mã đơn ngoài, trạng thái, giá bán và giá vốn đã chốt |
| Chuyển kho | StockTransfer, StockTransferItem | Xuất, đang vận chuyển, nhận hàng, chênh lệch |
| Kiểm kê | Stocktake, StocktakeItem | Số lượng hệ thống tại mốc kiểm kê, số đếm, điều chỉnh |
| Tiếp nhận sự kiện | WebhookEvent hoặc Inbox tương đương | Nhận diện trùng, trạng thái xử lý, lỗi và thứ tự sự kiện |
| Dự báo nhập hàng, nếu cần lưu kết quả | ForecastRun, ForecastResult hoặc mô hình tương đương | Phạm vi tenant/chi nhánh/SKU, kỳ dự báo, thời điểm dữ liệu, kết quả và phiên bản phương pháp |

Nếu cần gửi thông báo/tác vụ sau khi transaction hoàn tất một cách có thể phục hồi, có thể bổ sung cơ chế lưu sự kiện chờ phát như Outbox. Đây là phương án thiết kế để xử lý khoảng trống giữa lưu dữ liệu và gửi thông báo, không phải công nghệ bắt buộc được ghi trong đề bài.

### 8. Quy tắc bất biến nên dùng làm tiêu chí kiểm tra

Các quy tắc sau cần được cụ thể hóa theo những quyết định nghiệp vụ ở trên:

1. Trong các luồng bán/giữ hàng hợp lệ, `available = on_hand - reserved`, `reserved ≥ 0` và không cho phát sinh `available < 0`.
2. Nếu dùng số dư lưu sẵn, `on_hand` phải khớp tổng giao dịch IN trừ OUT của đúng tenant/chi nhánh/SKU, bao gồm giao dịch tồn đầu kỳ.
3. `reserved` phải khớp tổng phần giữ còn hiệu lực của các đơn tương ứng.
4. Mỗi lần chuyển trạng thái gây tác động tồn kho chỉ được tạo tác động đó một lần.
5. Không được sửa/xóa giao dịch sổ đã ghi; sửa sai bằng giao dịch bù trừ có liên kết và lý do.
6. Giá vốn đã chốt của dòng bán cũ không bị thay đổi chỉ vì có đợt nhập mới.
7. Dữ liệu liên quan trong cùng nghiệp vụ phải thuộc cùng tenant, kể cả quan hệ giữa đơn, chi nhánh và SKU.
8. Một nghiệp vụ lỗi phải hoàn tác toàn bộ phần dữ liệu thuộc transaction của bước đó.
9. Hàng chuyển kho phải đối soát được giữa nơi gửi, lượng đang vận chuyển và nơi nhận; mọi thiếu hụt có điều chỉnh được ghi nhận.

Đặc biệt, thiếu hụt kiểm kê là tình huống thực tế phải được xử lý riêng để giữ tính đúng của dữ liệu và giải quyết các phần giữ đã tồn tại, thay vì che giấu bằng một ràng buộc số học.

### 9. Các kịch bản kiểm thử và nghiệm thu quan trọng

Bảng dưới đây là các ca **đề xuất cần xây dựng**. Chưa có mã nguồn hoặc kết quả chạy thử nào được cung cấp để xác nhận chúng đã đạt.

| Kịch bản | Kết quả mong đợi | Nhóm yêu cầu |
|---|---|---|
| 50 yêu cầu đồng thời, còn 1 sản phẩm, mỗi yêu cầu mua 1 | Khi không có bổ sung/giải phóng tồn: đúng 1 yêu cầu giữ thành công, không âm tồn, không ghi tác động trùng | RSE, PERF |
| POS và đơn online cùng mua đơn vị cuối | Tổng số lượng chấp nhận không vượt tồn khả dụng | POS, RSE |
| Gửi lại cùng webhook hoặc bấm thanh toán hai lần | Không tạo thêm đơn/giữ hàng/xuất hàng cho cùng một lệnh nghiệp vụ | ORD, POS, SIM |
| Một đơn có nhiều SKU, một SKU thiếu hàng | Hoàn tác toàn bộ phần giữ nếu chính sách là nhận toàn bộ hoặc từ chối toàn bộ | ORD, RSE, SEC |
| Lỗi khi ghi ledger trong lúc xác nhận đơn | Không để đơn đã xác nhận nhưng tồn/giá vốn/sổ ở trạng thái dở dang | INV, COST, SEC |
| Hủy Reserved hai lần hoặc chạy lại tác vụ hết hạn | Chỉ giải phóng đúng một lần phần giữ còn hiệu lực | RSE, SIM |
| Xác nhận đơn đúng lúc tác vụ hết hạn chạy | Chỉ một kết quả hợp lệ; không vừa xuất hàng vừa giải phóng lặp | ORD, RSE, SIM |
| Hủy sau Confirmed | Tuân thủ quy trình hậu xác nhận đã thống nhất; không giảm reserved lần hai | ORD, INV |
| Nhập 10 × 100.000 rồi 5 × 130.000 | Giá vốn sau lần nhập thứ hai là 110.000 theo điều kiện ví dụ | COST |
| Bán hàng, sau đó nhập lô làm đổi giá vốn | Giá vốn trên dòng đơn cũ và lợi nhuận lịch sử không tự đổi | COST, REP |
| Xác nhận lại cùng phiếu nhập | Không tăng tồn/tính giá vốn lần hai | INV, COST |
| Chuyển A sang B, B chưa xác nhận | Theo dõi được hàng đang vận chuyển; chưa làm tăng khả dụng của B | INV |
| Kiểm kê thấp hơn lượng đã giữ | Kích hoạt quy trình xử lý thiếu hụt và phần giữ; không âm khả dụng âm thầm | INV, RSE |
| Tenant A dùng ID của đơn/SKU/chi nhánh thuộc B | Không đọc, sửa hoặc gắn quan hệ trái phạm vi tenant | AUTH, TENANT |
| Nhận thông báo ở phiên của tenant khác | Không nhận đơn hoặc thông tin thuộc tenant không được phép | TENANT, SIM |
| Ứng dụng thử UPDATE/DELETE sổ | Bị chặn theo cơ chế đã thiết kế; sửa sai qua giao dịch mới | INV, SEC |
| Tìm kiếm, tạo đơn, báo cáo ở tải đã thống nhất | Đạt các ngưỡng thời gian và ghi nhận phương pháp đo rõ ràng | PERF |
| Đánh giá dự báo AI trên lịch sử theo thời gian | Đo theo chỉ số/ngưỡng đã thống nhất; dữ liệu tương lai không được dùng để tạo dự báo cho quá khứ | Gói công việc 5; cần bổ sung mã yêu cầu |
| SKU mới, lịch sử ít hoặc tác vụ dự báo thất bại | Thể hiện rõ thiếu dữ liệu/lỗi theo chính sách đã chốt, không tự thay đổi tồn hoặc phát sinh đơn mua | Gói công việc 5; cần bổ sung mã yêu cầu |
| Chạy gói demo trên môi trường mới đáp ứng hướng dẫn | Backend, Web Admin, POS App, PostgreSQL và script dữ liệu mẫu hoạt động cùng nhau; các luồng demo chính chạy được | Mục f), gói công việc 5 |
| Thực hiện gọi API theo tài liệu Swagger/Postman | Hướng dẫn xác thực, quyền và ví dụ yêu cầu/phản hồi khớp với API thực tế | Mục f) |

### 10. Cách triển khai đồ án và chuẩn bị hồ sơ

#### 10.1. Đối chiếu năm gói công việc trong đề

| Gói | Trọng tâm theo đề | Điểm cần theo dõi khi phân công |
|---|---|---|
| 1 | Nền tảng .NET 8, Clean Architecture, PostgreSQL, multi-tenant, EF Core, JWT/RBAC | Liên kết đầy đủ FR-AUTH, bao gồm quản lý chi nhánh, và các NFR về cách ly dữ liệu |
| 2 | API sản phẩm/SKU/mã vạch/giá, ledger, nhập/chuyển kho, giá vốn | FR-PROD còn có danh mục/thương hiệu; FR-INV còn có kiểm kê. Các chức năng này vẫn thuộc yêu cầu dù mô tả gói không liệt kê riêng |
| 3 | Mô hình/trạng thái đơn, giữ–trừ hàng có khóa, simulator, Hangfire | Bao gồm tình huống hết hạn, xử lý đồng thời, rollback và các quy tắc xử lý lặp sau khi được bổ sung vào đặc tả |
| 4 | ReactJS Admin, ReactJS PWA POS, SignalR | Bao quát quyền màn hình và kết nối các API tương ứng; thao tác checkout nguyên tử phải được bảo đảm tại backend/cơ sở dữ liệu |
| 5 | Báo cáo, AI dự báo nhập hàng, kiểm thử, CI/CD, Docker, tài liệu | FR-REP còn có báo cáo tồn/tốc độ bán và cảnh báo; cần phân công đủ. AI cần yêu cầu nghiệm thu riêng |

SignalR được đề cập trong nhóm FR-SIM của gói 3 và triển khai rõ ở gói 4; nên thống nhất ai chịu trách nhiệm phát sự kiện, phân nhóm tenant và hiển thị thông báo. Tên gói công việc 4 chỉ nhắc POS nhưng nội dung bao gồm cả Admin Dashboard. Kiểm thử được liệt kê trong gói 5 không có nghĩa phải đợi đến cuối mới viết kiểm thử cho lõi kho và giữ hàng.

Các gói công việc mô tả phạm vi, chưa cung cấp số thành viên, thời lượng hoặc ngày bàn giao. Vì vậy, chưa có cơ sở để gán số tuần hoặc phân công số người cụ thể từ nội dung đề.

#### 10.2. Thứ tự triển khai gợi ý

Một thứ tự triển khai hợp lý, **là đề xuất tổ chức thực hiện năm gói công việc chứ không phải lịch bắt buộc trong bản gốc**, là:

1. **Chốt đặc tả:** Hoàn thiện trạng thái đơn, phạm vi giá vốn, mô hình số dư/sổ, tenant, trả hàng, chuyển kho, quy tắc thanh toán và phạm vi AI dự báo nhập hàng. Lập URS cùng RTM ngay từ giai đoạn này.
2. **Xây nền dữ liệu và quyền:** Tenant, xác thực, phân quyền, chi nhánh/kho, danh mục, sản phẩm và SKU.
3. **Hoàn thiện lõi kho:** Nhập hàng, sổ bất biến, số dư theo mô hình đã chọn, giá vốn và các kiểm thử đối soát.
4. **Hoàn thiện giữ hàng và đơn:** Kiểm soát đồng thời, trạng thái, idempotency, hủy/hết hạn, chốt giá vốn và rollback.
5. **Kết nối các luồng sử dụng:** ReactJS Admin/PWA POS, đơn quản trị, simulator, thông báo SignalR, tác vụ Hangfire; đồng thời hoàn thiện chuyển kho, kiểm kê và xử lý trả hàng theo phạm vi thống nhất.
6. **Báo cáo, AI và nghiệm thu:** Xây báo cáo/cảnh báo, tích hợp và đánh giá dự báo nhập hàng trên dữ liệu đã chuẩn bị; hoàn thiện kiểm thử tải, cách ly tenant, trải nghiệm POS, tài liệu API/cài đặt, Docker Compose kèm dữ liệu mẫu và CI/CD.

Lõi tồn kho và giữ hàng nên có bằng chứng chạy đúng sớm. Đây là phần dễ phát sinh sai lệch khi nhiều tác vụ cùng thao tác; giao diện có thể trông hoàn chỉnh ngay cả khi các lỗi này vẫn tồn tại.

Với RTM, mỗi mã FR/NFR nên truy được đến ca sử dụng, thành phần thiết kế, phần triển khai và ca kiểm thử có bằng chứng. Ví dụ: `FR-RSE-02 → Giữ hàng cho đơn online → Use case ReserveStock → các kiểm thử thiếu hàng/đồng thời/rollback`. NFR như thời gian phản hồi phải truy tới báo cáo đo, không chỉ một ảnh chụp màn hình.

Các sơ đồ nên tập trung vào: quan hệ tenant–chi nhánh–SKU; đơn–dòng đơn–phần giữ–sổ tồn; trình tự giữ hàng; xác nhận; POS nhanh; hủy/hết hạn; chuyển kho hai bước. Bổ sung luồng dữ liệu dự báo sau khi chốt phạm vi AI. Công nghệ frontend đã được xác định là ReactJS và danh sách sản phẩm bàn giao đã có đủ tại mục f); các chi tiết giao diện, cách chia dự án và triển khai AI vẫn cần thiết kế.

#### 10.3. Đối chiếu sản phẩm bàn giao

| Sản phẩm theo mục f) | Nội dung đã được xác định | Bằng chứng nghiệm thu đề xuất |
|---|---|---|
| Backend | .NET 8 Web API, xác thực đa tenant, ledger, chống bán vượt tồn, xử lý đơn thời gian thực | API chạy được cùng dữ liệu thực; kiểm thử nghiệp vụ, đồng thời, rollback và cách ly tenant |
| Admin Dashboard | Quản lý sản phẩm, xem ledger, xử lý đơn đa kênh, báo cáo lợi nhuận gộp; dùng ReactJS theo gói 4 | Người dùng có quyền thực hiện các luồng quản trị xuyên suốt từ giao diện đến backend |
| POS Application | ReactJS PWA, cảm ứng, quét mã, thanh toán nhanh | Bán tại quầy, bảo vệ hàng đã giữ, in biên nhận và kiểm tra bố cục trên các loại thiết bị yêu cầu |
| Tài liệu | Thiết kế kỹ thuật, đặc tả API Swagger/Postman, hướng dẫn triển khai; kết hợp danh sách ở mục e) | URS/RTM, UML/ERD, tài liệu kiểm thử và hướng dẫn khớp chức năng/API thực tế |
| Gói demo | Docker Compose có Backend, Web Admin, POS App, PostgreSQL và script seed data | Cài từ môi trường mới theo hướng dẫn; có dữ liệu mẫu để trình diễn các luồng chính |

AI được yêu cầu trong gói 5 nhưng chưa được phân bổ rõ vào sản phẩm giao diện/API nào ở mục f). Cần chỉ ra nơi người dùng xem khuyến nghị, cách chạy dự báo và dữ liệu dùng để demo. Dữ liệu mẫu minh họa chức năng không tự là bằng chứng mô hình dự báo chính xác trên dữ liệu kinh doanh thực tế.

### 11. Những quyết định cần xác nhận để hoàn thiện bản đặc tả

| Ưu tiên | Quyết định | Vì sao ảnh hưởng lớn |
|---|---|---|
| Trước khi thiết kế dữ liệu | Dùng schema riêng hay chung schema + TenantId? | Chi phối cách cách ly, truy vấn, migration và ràng buộc |
| Trước khi thiết kế dữ liệu | Số dư tồn được lưu và cập nhật có kiểm soát hay luôn tính từ sổ? | Giải quyết mâu thuẫn về on_hand, quyết định cách khóa và đối soát |
| Trước khi thiết kế dữ liệu | Giá vốn theo tenant hay chi nhánh; hàng đang vận chuyển được định giá thế nào? | Chi phối nhập mua, chuyển kho, báo cáo tồn và giá vốn |
| Trước khi viết luồng đơn | Confirmed/Completed nghĩa là gì; hủy được phép từ trạng thái nào? | Quyết định thời điểm xuất tồn, ghi nhận doanh thu và xử lý hủy |
| Trước khi viết luồng đơn | Đơn online và POS có những transaction nào? | Bảo đảm ACID đúng với các bước chạy ở thời điểm khác nhau |
| Trước khi viết luồng đơn | Giữ hàng hết hạn sau bao lâu; nhận đơn nhiều SKU theo toàn bộ hay từng phần? | Quyết định cách giữ, rollback và tác vụ nền |
| Trước khi nghiệm thu tích hợp | Chỉ simulator hay có kết nối sàn thật; Lazada nằm trong phạm vi nào? | Tránh đánh giá sản phẩm theo một phạm vi chưa được yêu cầu |
| Trước khi nghiệm thu POS | QR xác nhận thủ công hay tự động; có bán offline không? | Làm rõ thanh toán và khả năng bảo đảm tồn ở thời điểm bán |
| Trước khi nghiệm thu báo cáo | Doanh thu thuần, trả hàng, phí và thời điểm ghi nhận tính thế nào? | Bảo đảm báo cáo được kiểm tra bằng số liệu xác định |
| Trước khi triển khai AI | Dự báo gì, dữ liệu nào, hiển thị ở đâu và nghiệm thu theo tiêu chí nào? | Chuyển yêu cầu AI của gói 5 thành chức năng có thể triển khai, truy vết và đánh giá |
| Trước khi nghiệm thu cuối | Gói demo cần cấu hình/phụ thuộc nào và seed data phục vụ những kịch bản nào? | Bảo đảm đủ Backend, Web Admin, POS, PostgreSQL, tài liệu API và khả năng trình diễn tính năng AI |

**Đánh giá:** Bản đề đầy đủ xác định rõ các sản phẩm bàn giao, ReactJS cho hai giao diện, năm gói công việc và yêu cầu AI dự báo nhập hàng. Các trọng tâm kỹ thuật phù hợp để thể hiện năng lực xây dựng hệ thống có nghiệp vụ thực tế. Tuy nhiên, để trở thành đặc tả triển khai có thể nghiệm thu nhất quán, vẫn cần chốt số dư tồn, ranh giới transaction, trạng thái sau xác nhận, phạm vi giá vốn, các nghiệp vụ đảo ngược và tiêu chí đánh giá AI.
