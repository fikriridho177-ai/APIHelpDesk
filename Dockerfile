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
EXPOSE 8080

# Compatibility workaround for the legacy SQL Server pre-login TLS handshake.
# This is the runtime setting used by the previously working API image.
USER root
RUN sed -i '/\[openssl_init\]/a ssl_conf = ssl_sect' /etc/ssl/openssl.cnf
RUN printf "\n[ssl_sect]\nsystem_default = system_default_sect\n" >> /etc/ssl/openssl.cnf
RUN printf "\n[system_default_sect]\nMinProtocol = TLSv1\nCipherString = DEFAULT@SECLEVEL=0\n" >> /etc/ssl/openssl.cnf

COPY --from=build /app/publish .

# Firmware packages and Sensus photos are mounted or written at runtime.
# Prepare all writable paths for the non-root ASP.NET runtime user.
RUN mkdir -p /app/firmware /app/wwwroot/uploads/sensus \
    && chown -R $APP_UID:$APP_UID /app/firmware /app/wwwroot

USER $APP_UID
ENTRYPOINT ["dotnet", "AuthUtility.Api.dll"]
