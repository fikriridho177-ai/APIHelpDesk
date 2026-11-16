FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AuthUtility.Api/AuthUtility.Api.csproj", "src/AuthUtility.Api/"]
COPY ["src/AuthUtility.Application/AuthUtility.Application.csproj", "src/AuthUtility.Application/"]
COPY ["src/AuthUtility.Domain/AuthUtility.Domain.csproj", "src/AuthUtility.Domain/"]
COPY ["src/AuthUtility.Infrastructure/AuthUtility.Infrastructure.csproj", "src/AuthUtility.Infrastructure/"]

RUN dotnet restore "src/AuthUtility.Api/AuthUtility.Api.csproj"

COPY src/ ./src/
RUN dotnet publish "src/AuthUtility.Api/AuthUtility.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
# The production WAD/Logger SQL Server is a legacy instance. Keep this
# compatibility profile isolated to this API container instead of changing the
# base image's global OpenSSL configuration.
ENV OPENSSL_CONF=/app/openssl-legacy.cnf
EXPOSE 8080

COPY --from=build /app/publish .
COPY deployment/openssl-legacy.cnf /app/openssl-legacy.cnf

# Firmware packages and Sensus photos are mounted or written at runtime.
# Prepare all writable paths for the non-root ASP.NET runtime user.
RUN mkdir -p /app/firmware /app/wwwroot/uploads/sensus \
    && chown -R $APP_UID:$APP_UID /app/firmware /app/wwwroot

USER $APP_UID
ENTRYPOINT ["dotnet", "AuthUtility.Api.dll"]
