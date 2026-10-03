# Build context là backend/ (xem deploy/docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
# Cache NuGet dạng private: các image build song song mỗi bên một bản, không giẫm lên nhau khi restore.
RUN --mount=type=cache,id=oism-nuget,target=/root/.nuget/packages,sharing=private \
    dotnet publish services/channel/src/Oism.Channel.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# curl cho health check của Compose.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Oism.Channel.Api.dll"]
