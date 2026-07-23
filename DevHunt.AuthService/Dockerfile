# BUILD STAGE
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy .slnx and ALL .csproj at once to optimize layer caching
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY DevHunt.slnx .
COPY DevHunt.AuthService/DevHunt.AuthService.csproj DevHunt.AuthService/
COPY DevHunt.CoreApi/DevHunt.CoreApi.csproj DevHunt.CoreApi/
COPY DevHunt.Infrastructure/DevHunt.Infrastructure.csproj DevHunt.Infrastructure/

# Restore dependencies for the ENTIRE solution
RUN dotnet restore "DevHunt.slnx" --verbosity minimal

# Copy ALL source code
COPY . .

# Publish (dotnet publish will restore if needed)
# Removed --no-restore to avoid issues with missing packages
WORKDIR "/src/DevHunt.AuthService"
RUN dotnet publish "DevHunt.AuthService.csproj" -c Release -o /app/publish

# FINAL STAGE
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# SECURITY: Create non-root user (SEC-015)
# Combine commands to reduce number of layers
RUN groupadd -r appuser && useradd -r -g appuser appuser && \
    apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/* && \
    chown -R appuser:appuser /app

# Copy published application
COPY --from=build /app/publish .
RUN mkdir -p /app/.aspnet/DataProtection-Keys && \
    chown -R appuser:appuser /app

# SECURITY: Run as non-root user
USER appuser

ENTRYPOINT ["dotnet", "DevHunt.AuthService.dll"]
