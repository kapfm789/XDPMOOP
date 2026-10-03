-- Một database oism, mỗi service một schema (ADR-0013). Chỉ chạy khi volume dữ liệu còn trống.
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS channel;
CREATE SCHEMA IF NOT EXISTS insights;
