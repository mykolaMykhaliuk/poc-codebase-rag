# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/CodebaseRag.Api/CodebaseRag.Api.csproj", "CodebaseRag.Api/"]
RUN dotnet restore "CodebaseRag.Api/CodebaseRag.Api.csproj"

# Copy source code and build
COPY src/ .
WORKDIR /src/CodebaseRag.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

# Copy published app
COPY --from=build /app/publish .

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Set entrypoint
ENTRYPOINT ["dotnet", "CodebaseRag.Api.dll"]
