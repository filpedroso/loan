# Build from partner/: docker build -f ExternalPartner.Mock/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY ExternalPartner.Mock/ExternalPartner.Mock.csproj ExternalPartner.Mock/
RUN dotnet restore ExternalPartner.Mock/ExternalPartner.Mock.csproj
COPY ExternalPartner.Mock/ ExternalPartner.Mock/
RUN dotnet publish ExternalPartner.Mock/ExternalPartner.Mock.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ExternalPartner.Mock.dll"]
