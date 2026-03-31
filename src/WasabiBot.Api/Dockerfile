FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

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

RUN dotnet publish "src/WasabiBot.Api/WasabiBot.Api.csproj" -c ${BUILD_CONFIGURATION} -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WasabiBot.Api.dll"]
