# Use case: kênh sàn giả lập và thông báo realtime

Hai use case của `channel` và phần SignalR trong `insights`. Thiết kế: [nhận webhook](../design/flows/webhook-ingestion.md), [API channel](../design/api/channel.md), [API insights](../design/api/insights.md).

## UC-SIM-01 Bắn webhook giả lập

- Actor: Owner, trong vai người phát triển hoặc kiểm thử
- Yêu cầu: FR-SIM-01
- API: `POST /api/channel/webhooks/{channel}`, `POST /api/channel/simulator/burst`, `GET /api/channel/webhook-events`, `GET, POST /api/channel/channel-shops`

Là người kiểm thử, tôi muốn gửi đơn giả lập từ Shopee, TikTok, Lazada, kể cả hàng chục đơn cùng lúc, để thử khả năng xử lý và cơ chế khóa khi giữ hàng.

Tiêu chí chấp nhận:

1. Cho shop đã cấu hình và chữ ký đúng, khi gửi webhook, thì trả 202, một bản ghi webhook được lưu và command `SubmitOrder` được phát.
2. Cho chữ ký sai, khi gửi webhook, thì trả 401 và không lưu gì.
3. Cho shop chưa cấu hình, khi gửi webhook, thì trả 404.
4. Cho cùng mã sự kiện gửi lại, thì trả 202 với trạng thái của lần đầu; không có command thứ hai (T03).
5. Cho yêu cầu bắn 50 đơn đồng thời vào một SKU, khi chạy, thì 50 webhook được gửi song song; đây là công cụ để chạy T01.
6. Cho danh sách webhook, khi xem, thì mỗi bản ghi có trạng thái Received, Submitted, Reserved, Rejected hoặc Failed, kèm lý do khi bị từ chối.
7. Payload của ba kênh có hình dạng khác nhau; adapter của từng kênh đổi về cùng một Canonical Order.

## UC-SIM-02 Nhận thông báo đơn mới theo thời gian thực

- Actor: Owner, Staff, Cashier
- Yêu cầu: FR-SIM-02
- Kết nối: WebSocket `/hubs/notifications`

Là nhân viên, tôi muốn thấy đơn mới ngay khi nó tới, để xử lý mà không phải tải lại trang.

Tiêu chí chấp nhận:

1. Cho đơn online vừa được giữ hàng, thì admin và POS của đúng tenant nhận thông báo có âm thanh và popup trong vài giây, không cần tải lại trang.
2. Cho người dùng gắn với một chi nhánh, thì chỉ nhận thông báo của chi nhánh đó; Owner và Staff nhận của mọi chi nhánh trong tenant.
3. Cho phiên của tenant khác, thì không nhận thông báo này (T15).
4. Cho kết nối bị rớt, thì ứng dụng tự nối lại; thông báo phát trong lúc mất kết nối không được gửi bù.
5. Kết nối WebSocket phải mang access token hợp lệ; không có token thì bị từ chối.
