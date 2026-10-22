FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["src/ZFome.Infrastructure.Migrations/ZFome.Infrastructure.Migrations.csproj", "src/ZFome.Infrastructure.Migrations/"]
COPY ["src/ZFome.Infrastructure/ZFome.Infrastructure.csproj", "src/ZFome.Infrastructure/"]
COPY ["src/ZFome.Application/ZFome.Application.csproj", "src/ZFome.Application/"]
COPY ["src/ZFome.Domain/ZFome.Domain.csproj", "src/ZFome.Domain/"]
RUN dotnet restore "src/ZFome.Infrastructure.Migrations/ZFome.Infrastructure.Migrations.csproj"

COPY . .
WORKDIR /src/src/ZFome.Infrastructure.Migrations
RUN dotnet publish "ZFome.Infrastructure.Migrations.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --no-install-recommends -y libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ZFome.Infrastructure.Migrations.dll"]
