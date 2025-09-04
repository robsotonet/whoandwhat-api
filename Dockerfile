# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restore
COPY ["src/WhoAndWhat.API/WhoAndWhat.API.csproj", "src/WhoAndWhat.API/"]
COPY ["src/WhoAndWhat.Application/WhoAndWhat.Application.csproj", "src/WhoAndWhat.Application/"]
COPY ["src/WhoAndWhat.Infrastructure/WhoAndWhat.Infrastructure.csproj", "src/WhoAndWhat.Infrastructure/"]
COPY ["src/WhoAndWhat.Domain/WhoAndWhat.Domain.csproj", "src/WhoAndWhat.Domain/"]
COPY ["Directory.Build.props", "./"]

# Restore dependencies
RUN dotnet restore "src/WhoAndWhat.API/WhoAndWhat.API.csproj"

# Copy source code
COPY . .

# Build the application
RUN dotnet build "src/WhoAndWhat.API/WhoAndWhat.API.csproj" -c Release --no-restore

# Publish stage
FROM build AS publish
RUN dotnet publish "src/WhoAndWhat.API/WhoAndWhat.API.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user for security
RUN groupadd -r dotnet && useradd -r -g dotnet dotnet

# Copy published application
COPY --from=publish /app/publish .

# Set ownership and permissions
RUN chown -R dotnet:dotnet /app
USER dotnet

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080;https://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "WhoAndWhat.API.dll"]