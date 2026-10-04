# Hợp đồng thông điệp

Có 8 thông điệp đi giữa các service: 7 event và 1 command. Kiểu C# của chúng nằm ở `backend/shared/Oism.Contracts`; file này là nguồn chuẩn cho tên, trường và ý nghĩa. Cơ chế phát và nhận nằm ở [architecture/messaging.md](../architecture/messaging.md).

| Thông điệp | Loại | Phát | Nhận | Khi nào |
| --- | --- | --- | --- | --- |
| `BranchUpserted` | Event trạng thái | identity | core | Tạo, sửa, bật hoặc tắt chi nhánh |
| `SkuUpserted` | Event trạng thái | catalog | core, insights | Tạo hoặc sửa SKU, mã vạch, giá |
| `SubmitOrder` | Command | channel | core | Một webhook đơn hàng hợp lệ và chưa từng thấy |
| `OrderReserved` | Event | core | channel, insights | Đơn online hoặc đơn thủ công được giữ hàng |
| `OrderRejected` | Event | core | channel, insights | Đơn online không giữ được hàng |
| `OrderConfirmed` | Event | core | insights | Đơn sang Confirmed, kể cả đơn POS |
| `OrderCancelled` | Event | core | insights | Đơn sang Cancelled do hủy tay hoặc hết hạn |
| `StockChanged` | Event trạng thái | core | insights | Bất kỳ thay đổi nào của một dòng số dư hoặc ngưỡng |

Mọi thông điệp nằm trong phong bì chung có `eventId`, `type`, `tenantId`, `occurredAt`. Các bảng dưới chỉ liệt kê `payload`.

## BranchUpserted

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `branchId` | uuid | |
| `code`, `name` | string | |
| `type` | string | `Store`, `Warehouse` |
| `isActive` | boolean | |
| `version` | long | Bên nhận bỏ qua nếu nhỏ hơn hoặc bằng bản đang giữ |

## SkuUpserted

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `skuId`, `productId` | uuid | |
| `skuCode` | string | |
| `name` | string | Tên hiển thị gồm thuộc tính |
| `barcodes` | string[] | Mọi mã vạch đang gán |
| `retailPrice`, `wholesalePrice` | decimal | |
| `isActive` | boolean | False khi SKU hoặc sản phẩm của nó ngừng bán |
| `version` | long | Bên nhận bỏ qua nếu nhỏ hơn hoặc bằng bản đang giữ |

## SubmitOrder

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `channel` | string | `Shopee`, `TikTok`, `Lazada` |
| `externalOrderId` | string | Khóa chống trùng ở `core` |
| `branchId` | uuid | Lấy từ cấu hình shop |
| `placedAt` | datetime | Thời điểm đặt trên sàn |
| `lines` | mảng | Mỗi phần tử: `skuCode`, `quantity`, `unitPrice`, `discount` |

`core` tra SKU theo `skuCode` trong `sku_refs`. Không tìm thấy hoặc SKU ngừng bán thì phát `OrderRejected`.

## OrderReserved

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `orderId` | uuid | |
| `orderNumber` | string | |
| `channel` | string | |
| `externalOrderId` | string, cho phép null | Null với đơn thủ công |
| `branchId` | uuid | |
| `totalAmount` | decimal | |
| `reservedUntil` | datetime | |
| `lines` | mảng | Mỗi phần tử: `skuId`, `quantity` |

## OrderRejected

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `channel`, `externalOrderId` | string | |
| `branchId` | uuid | |
| `reason` | string | `InsufficientStock`, `UnknownSku`, `InactiveSku`, `InactiveBranch` |
| `details` | mảng | Mỗi phần tử: `skuCode`, `requested`, `available` |

## OrderConfirmed

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `orderId` | uuid | |
| `channel` | string | Gồm cả `POS` và `Admin` |
| `branchId` | uuid | |
| `confirmedAt` | datetime | Mốc ghi nhận của báo cáo |
| `lines` | mảng | Mỗi phần tử: `orderItemId`, `skuId`, `quantity`, `unitPrice`, `discount`, `costPrice` |

`costPrice` là giá vốn đã chốt. `insights` không bao giờ tự tính lại giá vốn.

## OrderCancelled

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `orderId` | uuid | |
| `channel` | string | |
| `externalOrderId` | string, cho phép null | |
| `branchId` | uuid | |
| `reason` | string | `Manual`, `Expired` |
| `cancelledAt` | datetime | |

## StockChanged

| Trường | Kiểu | Ghi chú |
| --- | --- | --- |
| `branchId`, `skuId` | uuid | |
| `onHand`, `reserved`, `available` | int | Trạng thái sau thay đổi |
| `avgCost` | decimal | |
| `threshold` | int, cho phép null | |
| `version` | long | Bằng `inventory_balances.version` |

## Ví dụ

```json
{
  "eventId": "5c0d7d7e-2f0a-4a8e-9b63-1f8f0a2c3d10",
  "type": "OrderConfirmed",
  "tenantId": "8a0b7c2e-1f3d-4b6a-9c11-5d2e7f809a44",
  "occurredAt": "2026-10-20T03:15:27Z",
  "payload": {
    "orderId": "d2b1f0a4-6c3e-4f5a-8b7d-9e0f1a2b3c4d",
    "channel": "Shopee",
    "branchId": "1e2d3c4b-5a69-4788-9a0b-c1d2e3f4a5b6",
    "confirmedAt": "2026-10-20T03:15:27Z",
    "lines": [
      {
        "orderItemId": "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
        "skuId": "0f9e8d7c-6b5a-4c3d-9e2f-1a0b9c8d7e6f",
        "quantity": 2,
        "unitPrice": 150000,
        "discount": 0,
        "costPrice": 110000
      }
    ]
  }
}
```

## Thông báo SignalR

Đây không phải thông điệp giữa các service mà là thông báo `insights` đẩy xuống trình duyệt qua hub `/hubs/notifications`.

| Tên | Gửi tới group | Trường |
| --- | --- | --- |
| `OrderCreated` | `tenant:{tenantId}` và `tenant:{tenantId}:branch:{branchId}` | `orderId`, `orderNumber`, `channel`, `branchId`, `totalAmount` |
| `LowStock` | `tenant:{tenantId}` | `branchId`, `skuId`, `skuCode`, `name`, `available`, `threshold` |

`OrderCreated` phát khi `insights` nhận `OrderReserved`. `LowStock` phát khi `StockChanged` đưa `available` từ trên ngưỡng xuống bằng hoặc dưới ngưỡng.
