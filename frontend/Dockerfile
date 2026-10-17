# Image của admin hoặc pos cho gói demo (W5-04). Build context là frontend/ (xem deploy/docker-compose.demo.yml).
# APP chọn ứng dụng: admin hoặc pos. VITE_API_URL là địa chỉ gateway mà trình duyệt gọi, gắn cứng vào bản build.
FROM node:22-bookworm-slim AS build
ARG APP
ARG VITE_API_URL=http://localhost:8080
WORKDIR /src
# Cài package trước khi chép mã nguồn, để lớp cache của npm ci chỉ đổi khi lockfile đổi.
COPY package.json package-lock.json ./
COPY shared/package.json shared/
COPY admin/package.json admin/
COPY pos/package.json pos/
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build -w @oism/${APP}

FROM nginx:1.27-alpine
ARG APP
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/${APP}/dist /usr/share/nginx/html
