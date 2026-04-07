FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM oven/bun:1.2.9 AS css
WORKDIR /src/src/WasabiBot.Api

COPY ["src/WasabiBot.Api/package.json", "src/WasabiBot.Api/bun.lock", "./"]
RUN bun install --frozen-lockfile

COPY src/WasabiBot.Api/ ./
RUN bun run build:css

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["src/WasabiBot.Api/WasabiBot.Api.csproj", "src/WasabiBot.Api/"]
COPY ["src/WasabiBot.Api.Generators/WasabiBot.Api.Generators.csproj", "src/WasabiBot.Api.Generators/"]
COPY ["src/WasabiBot.ServiceDefaults/WasabiBot.ServiceDefaults.csproj", "src/WasabiBot.ServiceDefaults/"]

RUN dotnet restore "src/WasabiBot.Api/WasabiBot.Api.csproj"

COPY src/WasabiBot.Api/ src/WasabiBot.Api/
COPY src/WasabiBot.Api.Generators/ src/WasabiBot.Api.Generators/
COPY src/WasabiBot.ServiceDefaults/ src/WasabiBot.ServiceDefaults/
COPY --from=css /src/src/WasabiBot.Api/wwwroot/app.css src/WasabiBot.Api/wwwroot/app.css

RUN dotnet publish "src/WasabiBot.Api/WasabiBot.Api.csproj" -c ${BUILD_CONFIGURATION} -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WasabiBot.Api.dll"]
