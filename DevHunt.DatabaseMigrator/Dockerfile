FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY ["DevHunt.Infrastructure/DevHunt.Infrastructure.csproj", "DevHunt.Infrastructure/"]
COPY ["DevHunt.DatabaseMigrator/DevHunt.DatabaseMigrator.csproj", "DevHunt.DatabaseMigrator/"]

# Restore dependencies
RUN dotnet restore "DevHunt.DatabaseMigrator/DevHunt.DatabaseMigrator.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/DevHunt.DatabaseMigrator"

# Ensure publish/build can use cached restore in case new references were added after initial restore
RUN dotnet restore "DevHunt.DatabaseMigrator.csproj" --verbosity minimal && \
    dotnet build "DevHunt.DatabaseMigrator.csproj" -c Release -o /app/build --no-restore

# Publish the application
FROM build AS publish
RUN dotnet publish "DevHunt.DatabaseMigrator.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

COPY --from=publish /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

USER appuser

ENTRYPOINT ["dotnet", "DevHunt.DatabaseMigrator.dll"]

