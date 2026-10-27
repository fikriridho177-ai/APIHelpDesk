# syntax=docker/dockerfile:1

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

USER $APP_UID
ENTRYPOINT ["dotnet", "AuthUtility.Api.dll"]
