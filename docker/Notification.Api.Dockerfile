# syntax=docker/dockerfile:1
FROM node:24-alpine AS admin-build
WORKDIR /workspace/admin-ui

COPY admin-ui/package.json admin-ui/package-lock.json ./
RUN npm install

COPY admin-ui/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first (layer-cache friendly)
COPY notification-platform.slnx ./
COPY src/Notification.Domain/Notification.Domain.csproj                              src/Notification.Domain/
COPY src/Notification.Infrastructure/Notification.Infrastructure.csproj               src/Notification.Infrastructure/
COPY src/Notification.Persistence/Notification.Persistence.csproj                     src/Notification.Persistence/
COPY src/Notification.Templates/Notification.Templates.csproj                         src/Notification.Templates/
COPY src/Notification.Api/Notification.Api.csproj                                     src/Notification.Api/

RUN dotnet restore src/Notification.Api/Notification.Api.csproj

# Copy the rest and publish
COPY src/Notification.Domain/      src/Notification.Domain/
COPY src/Notification.Infrastructure/ src/Notification.Infrastructure/
COPY src/Notification.Persistence/   src/Notification.Persistence/
COPY src/Notification.Templates/     src/Notification.Templates/
COPY src/Notification.Api/         src/Notification.Api/

# RUN dotnet publish src/Notification.Api/Notification.Api.csproj \
#     -c Release -o /app/publish --no-restore
RUN dotnet publish src/Notification.Api/Notification.Api.csproj \
    -c Release -o /app/publish

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .
COPY --from=admin-build /workspace/src/Notification.Api/wwwroot/admin ./wwwroot/admin

ENTRYPOINT ["dotnet", "Notification.Api.dll"]
