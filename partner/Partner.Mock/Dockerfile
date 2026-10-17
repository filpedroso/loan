# Build from partner/: docker build -f Partner.Mock/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY Partner.Mock/Partner.Mock.csproj Partner.Mock/
RUN dotnet restore Partner.Mock/Partner.Mock.csproj
COPY Partner.Mock/ Partner.Mock/
RUN dotnet publish Partner.Mock/Partner.Mock.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Partner.Mock.dll"]
