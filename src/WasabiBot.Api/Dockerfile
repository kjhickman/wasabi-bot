FROM mcr.microsoft.com/dotnet/runtime-deps:11.0.0-preview.3 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.3 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["global.json", "."]
COPY ["src/WasabiBot.Api/WasabiBot.Api.csproj", "src/WasabiBot.Api/"]
COPY ["src/WasabiBot.Api.Generators/WasabiBot.Api.Generators.csproj", "src/WasabiBot.Api.Generators/"]
COPY ["src/WasabiBot.ServiceDefaults/WasabiBot.ServiceDefaults.csproj", "src/WasabiBot.ServiceDefaults/"]

RUN dotnet restore "src/WasabiBot.Api/WasabiBot.Api.csproj"

COPY src/WasabiBot.Api/ src/WasabiBot.Api/
COPY src/WasabiBot.Api.Generators/ src/WasabiBot.Api.Generators/
COPY src/WasabiBot.ServiceDefaults/ src/WasabiBot.ServiceDefaults/

RUN dotnet publish "src/WasabiBot.Api/WasabiBot.Api.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -r linux-$(if [ "$TARGETARCH" = "amd64" ]; then echo "x64"; else echo "$TARGETARCH"; fi) \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["./WasabiBot.Api"]
