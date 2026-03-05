# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["API.csproj", "./"]
RUN dotnet restore "API.csproj"

# Copy everything (including JSON files and Properties/launchSettings.json) and publish
COPY . .
RUN dotnet publish "API.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Listen on port 5207 (matches launchSettings.json http port)
ENV ASPNETCORE_URLS="http://+:5207"
EXPOSE 5207

# Copy published output
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "API.dll"]
