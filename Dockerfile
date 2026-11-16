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

COPY --from=build /app/publish .

# Firmware packages are mounted as a persistent volume in Compose/Dokploy.
# Prepare the default path for the non-root ASP.NET runtime user.
RUN mkdir -p /app/firmware && chown -R $APP_UID:$APP_UID /app/firmware

USER $APP_UID
ENTRYPOINT ["dotnet", "AuthUtility.Api.dll"]
