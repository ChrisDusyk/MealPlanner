# Use the official image as a parent image.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MealPlanner/MealPlanner.fsproj", "./"]
RUN dotnet restore "./MealPlanner.fsproj"
COPY MealPlanner/. .
WORKDIR "/src/."
RUN dotnet build "MealPlanner/MealPlanner.fsproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "MealPlanner.fsproj" -c Release -o /app/publish

# Final stage / image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MealPlanner.dll"]