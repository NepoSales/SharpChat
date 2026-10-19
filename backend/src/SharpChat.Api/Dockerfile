# Build context: backend/ (see docker-compose.yml at the repository root)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore first so the layer is cached while only source files change
COPY SharpChat.sln Directory.Build.props Directory.Packages.props ./
COPY src/SharpChat.Domain/SharpChat.Domain.csproj src/SharpChat.Domain/
COPY src/SharpChat.Application/SharpChat.Application.csproj src/SharpChat.Application/
COPY src/SharpChat.Infrastructure/SharpChat.Infrastructure.csproj src/SharpChat.Infrastructure/
COPY src/SharpChat.Api/SharpChat.Api.csproj src/SharpChat.Api/
RUN dotnet restore src/SharpChat.Api/SharpChat.Api.csproj

COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/SharpChat.Api/SharpChat.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SharpChat.Api.dll"]
