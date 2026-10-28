# Build context: folder APIHelpDesk
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dulu supaya layer package ter-cache
COPY src/AuthUtility.Api/AuthUtility.Api.csproj src/AuthUtility.Api/
COPY src/AuthUtility.Application/AuthUtility.Application.csproj src/AuthUtility.Application/
COPY src/AuthUtility.Domain/AuthUtility.Domain.csproj src/AuthUtility.Domain/
COPY src/AuthUtility.Infrastructure/AuthUtility.Infrastructure.csproj src/AuthUtility.Infrastructure/
RUN dotnet restore src/AuthUtility.Api/AuthUtility.Api.csproj

COPY src/ src/
RUN dotnet publish src/AuthUtility.Api/AuthUtility.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Program.cs membaca ASPNETCORE_URLS (default 127.0.0.1:5100, tidak bisa diakses dari luar container)
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "AuthUtility.Api.dll"]
