# Build from backend/: docker build -f src/Loans.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props nuget.config ./
COPY src/Loans.Domain/Loans.Domain.csproj src/Loans.Domain/
COPY src/Loans.UseCases/Loans.UseCases.csproj src/Loans.UseCases/
COPY src/Loans.Infrastructure/Loans.Infrastructure.csproj src/Loans.Infrastructure/
COPY src/Loans.Api/Loans.Api.csproj src/Loans.Api/
RUN dotnet restore src/Loans.Api/Loans.Api.csproj
COPY src/ src/
RUN dotnet publish src/Loans.Api/Loans.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Loans.Api.dll"]
