# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY notification-platform.slnx ./
COPY src/Notification.Domain/Notification.Domain.csproj                                     src/Notification.Domain/
COPY src/Notification.Infrastructure/Notification.Infrastructure.csproj                     src/Notification.Infrastructure/
COPY src/Notification.Templates/Notification.Templates.csproj                               src/Notification.Templates/
COPY src/Notification.Providers.Email.Smtp/Notification.Providers.Email.Smtp.csproj         src/Notification.Providers.Email.Smtp/
COPY src/Notification.Providers.Email.SendGrid/Notification.Providers.Email.SendGrid.csproj src/Notification.Providers.Email.SendGrid/
COPY src/Notification.Providers.Push.Firebase/Notification.Providers.Push.Firebase.csproj   src/Notification.Providers.Push.Firebase/
COPY src/Notification.Persistence/Notification.Persistence.csproj                           src/Notification.Persistence/
COPY src/Notification.Worker/Notification.Worker.csproj                                     src/Notification.Worker/

RUN dotnet restore src/Notification.Worker/Notification.Worker.csproj

COPY src/Notification.Domain/                    src/Notification.Domain/
COPY src/Notification.Infrastructure/            src/Notification.Infrastructure/
COPY src/Notification.Templates/                 src/Notification.Templates/
COPY src/Notification.Providers.Email.Smtp/      src/Notification.Providers.Email.Smtp/
COPY src/Notification.Providers.Email.SendGrid/  src/Notification.Providers.Email.SendGrid/
COPY src/Notification.Providers.Push.Firebase/   src/Notification.Providers.Push.Firebase/
COPY src/Notification.Persistence/               src/Notification.Persistence/
COPY src/Notification.Worker/                    src/Notification.Worker/

# RUN dotnet publish src/Notification.Worker/Notification.Worker.csproj \
#     -c Release -o /app/publish --no-restore

    RUN dotnet publish src/Notification.Worker/Notification.Worker.csproj \
    -c Release -o /app/publish 

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

# Templates are mounted at /app/templates via volume
VOLUME ["/app/templates", "/credentials"]

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Notification.Worker.dll"]
