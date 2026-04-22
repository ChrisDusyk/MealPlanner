# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project files and restore as distinct layers
COPY MealPlanner.slnx .
COPY Directory.Packages.props .
COPY MealPlanner.MigrationService/MealPlanner.MigrationService.csproj MealPlanner.MigrationService/
COPY MealPlanner.Api/MealPlanner.Api.csproj MealPlanner.Api/
COPY MealPlanner.AppHost/MealPlanner.AppHost.csproj MealPlanner.AppHost/
COPY MealPlanner.ServiceDefaults/MealPlanner.ServiceDefaults.csproj MealPlanner.ServiceDefaults/

# Restore dependencies
RUN dotnet restore MealPlanner.MigrationService/MealPlanner.MigrationService.csproj

# Copy source code
COPY MealPlanner.MigrationService/ MealPlanner.MigrationService/
COPY MealPlanner.Api/ MealPlanner.Api/
COPY MealPlanner.ServiceDefaults/ MealPlanner.ServiceDefaults/

# Build and publish
WORKDIR /source/MealPlanner.MigrationService
RUN dotnet publish -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MealPlanner.MigrationService.dll"]
