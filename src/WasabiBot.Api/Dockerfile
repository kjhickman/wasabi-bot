FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGET_RUNTIME=linux-arm64
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/WasabiBot.Api/WasabiBot.Api.csproj", "src/WasabiBot.Api/"]
COPY ["src/WasabiBot.DataAccess/WasabiBot.DataAccess.csproj", "src/WasabiBot.DataAccess/"]
COPY ["src/WasabiBot.ServiceDefaults/WasabiBot.ServiceDefaults.csproj", "src/WasabiBot.ServiceDefaults/"]

RUN dotnet restore "src/WasabiBot.Api/WasabiBot.Api.csproj" -r ${TARGET_RUNTIME}

COPY src/WasabiBot.Api/ src/WasabiBot.Api/
COPY src/WasabiBot.DataAccess/ src/WasabiBot.DataAccess/
COPY src/WasabiBot.ServiceDefaults/ src/WasabiBot.ServiceDefaults/

RUN dotnet publish "src/WasabiBot.Api/WasabiBot.Api.csproj" -c ${BUILD_CONFIGURATION} -r ${TARGET_RUNTIME} --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WasabiBot.Api.dll"]
