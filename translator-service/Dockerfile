FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY translator-service/ ./

RUN dotnet restore translator-service.csproj
RUN dotnet publish translator-service.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "translator-service.dll"]

