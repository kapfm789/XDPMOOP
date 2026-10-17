-- W5-03, T17: thêm 100.000 dòng bán vào insights.sales_facts của tenant seed Tạp hóa Xanh, để đo báo cáo doanh thu với
-- hơn 100.000 dòng (docs/testing/load-test.md). Chỉ dùng cho dữ liệu đo trên gói Compose, không phải đường dữ liệu của hệ thống:
-- sales_facts là bảng đọc, các dòng này không có đơn tương ứng ở core.
-- Chạy lại không thêm dòng mới: order_item_id cố định theo số thứ tự dòng.
--   docker compose -f deploy/docker-compose.demo.yml exec -T postgres psql -U oism -d oism < tools/k6/prepare-sales.sql
\set ON_ERROR_STOP on

DO $$
DECLARE
    tenant uuid := (SELECT tenant_id FROM identity.users WHERE email = 'owner@xanh.test');
    sku_count int;
BEGIN
    IF tenant IS NULL THEN
        RAISE EXCEPTION 'Seed tenant not found: run the seed first';
    END IF;

    -- Chi nhánh và SKU có thật của tenant, lấy từ bảng đọc của chính insights.
    CREATE TEMP TABLE k6_skus ON COMMIT DROP AS
        SELECT branch_id, sku_id, (row_number() OVER (ORDER BY branch_id, sku_id) - 1)::int AS n
        FROM insights.stock_snapshots
        WHERE tenant_id = tenant;
    sku_count := (SELECT count(*) FROM k6_skus);
    IF sku_count = 0 THEN
        RAISE EXCEPTION 'Seed tenant has no stock snapshots yet: wait for the seed to finish';
    END IF;

    -- 60 ngày gần nhất trước hôm nay, mỗi đơn 3 dòng, giờ bán rải từ 01:00 tới 15:00 UTC (08:00 tới 22:00 giờ Việt Nam).
    INSERT INTO insights.sales_facts
        (order_item_id, tenant_id, order_id, branch_id, sku_id, channel, quantity, revenue, cogs, confirmed_at)
    SELECT md5('k6-sales-line-' || i)::uuid, tenant, md5('k6-sales-order-' || (i / 3))::uuid,
           k6_skus.branch_id, k6_skus.sku_id,
           (ARRAY['POS', 'Shopee', 'TikTok', 'Lazada', 'Admin'])[1 + i % 5],
           1 + i % 3, (1 + i % 3) * 20000, (1 + i % 3) * 14000,
           date_trunc('day', now()) - (1 + i % 60) * interval '1 day' + interval '1 hour' + (i % 50400) * interval '1 second'
    FROM generate_series(1, 100000) AS i
    JOIN k6_skus ON k6_skus.n = i % sku_count
    ON CONFLICT (order_item_id) DO NOTHING;

    -- Báo cáo đọc daily_aggregates cho ngày đã tổng hợp. Xóa của tenant này để lần đo đọc thẳng sales_facts cho cả 60 ngày,
    -- tức trường hợp chậm nhất; job đêm dựng lại ngày hôm qua ở lần chạy sau.
    DELETE FROM insights.daily_aggregates WHERE tenant_id = tenant;
END $$;

SELECT count(*) AS sales_facts_of_tenant
FROM insights.sales_facts
WHERE tenant_id = (SELECT tenant_id FROM identity.users WHERE email = 'owner@xanh.test');
