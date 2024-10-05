FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-jammy-chiseled-aot AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/nightly/sdk:8.0-jammy-aot AS build
WORKDIR /src
COPY ["src/WasabiBot/WasabiBot.csproj", "src/WasabiBot/"]
RUN dotnet restore "src/WasabiBot/WasabiBot.csproj"
COPY . .
WORKDIR "/src/src/WasabiBot"
RUN dotnet build "WasabiBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WasabiBot.csproj" -c Release -o /app/publish

FROM base AS final
COPY --from=public.ecr.aws/awsguru/aws-lambda-adapter:0.8.4 /lambda-adapter /opt/extensions/lambda-adapter
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/app/WasabiBot"]
