# Build from the repository root: docker build -f partner/Partner.Mock/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props nuget.config ./
COPY partner/Partner.Mock/Partner.Mock.csproj partner/Partner.Mock/
RUN dotnet restore partner/Partner.Mock/Partner.Mock.csproj
COPY partner/Partner.Mock/ partner/Partner.Mock/
RUN dotnet publish partner/Partner.Mock/Partner.Mock.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Partner.Mock.dll"]
