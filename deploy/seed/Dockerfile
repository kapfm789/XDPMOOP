# Image của seed cho gói demo (W5-04). Build context là gốc repo (xem deploy/docker-compose.demo.yml): seed tham chiếu
# backend/shared/Oism.Contracts. Chỉ chép đúng các phần cần, phần còn lại của repo lọc bằng Dockerfile.dockerignore.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/
COPY backend/shared/Oism.Contracts backend/shared/Oism.Contracts
COPY deploy/seed deploy/seed
RUN --mount=type=cache,id=oism-nuget,target=/root/.nuget/packages,sharing=private \
    dotnet publish deploy/seed -c Release -o /app

# Seed là ứng dụng console; dùng chung image runtime với các service để không phải tải thêm image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Oism.Seed.dll"]
