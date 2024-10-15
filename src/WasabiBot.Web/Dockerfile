FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-jammy-chiseled-aot AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/nightly/sdk:8.0-jammy-aot AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/WasabiBot.Web/WasabiBot.Web.csproj", "src/WasabiBot.Web/"]
COPY ["src/WasabiBot.DataAccess/WasabiBot.DataAccess.csproj", "src/WasabiBot.DataAccess/"]
COPY ["src/WasabiBot.Core/WasabiBot.Core.csproj", "src/WasabiBot.Core/"]
RUN dotnet restore "src/WasabiBot.Web/WasabiBot.Web.csproj"
COPY . .
WORKDIR "/src/src/WasabiBot.Web"
RUN dotnet build "WasabiBot.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "WasabiBot.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/app/WasabiBot.Web"]
