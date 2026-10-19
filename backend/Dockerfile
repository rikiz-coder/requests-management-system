# Multi-stage build for the backend. One build stage, two runnable targets:
#   api     – ASP.NET Core Web API
#   seeder  – applies migrations and (re)creates the test data

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached while only source files change).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/RequestsManagement.Application/RequestsManagement.Application.csproj src/RequestsManagement.Application/
COPY src/RequestsManagement.Infrastructure/RequestsManagement.Infrastructure.csproj src/RequestsManagement.Infrastructure/
COPY src/RequestsManagement.Api/RequestsManagement.Api.csproj src/RequestsManagement.Api/
COPY tools/RequestsManagement.Seeder/RequestsManagement.Seeder.csproj tools/RequestsManagement.Seeder/
RUN dotnet restore src/RequestsManagement.Api/RequestsManagement.Api.csproj \
 && dotnet restore tools/RequestsManagement.Seeder/RequestsManagement.Seeder.csproj

COPY src/ src/
COPY tools/ tools/
RUN dotnet publish src/RequestsManagement.Api/RequestsManagement.Api.csproj -c Release --no-restore -o /out/api \
 && dotnet publish tools/RequestsManagement.Seeder/RequestsManagement.Seeder.csproj -c Release --no-restore -o /out/seeder

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RequestsManagement.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS seeder
WORKDIR /app
COPY --from=build /out/seeder .
USER $APP_UID
ENTRYPOINT ["dotnet", "RequestsManagement.Seeder.dll"]
