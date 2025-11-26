FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY routeestimation-service/ ./

RUN dotnet restore routeestimation-service.csproj
RUN dotnet publish routeestimation-service.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build /src/Datasets ./Datasets

ENTRYPOINT ["dotnet", "routeestimation-service.dll"]

