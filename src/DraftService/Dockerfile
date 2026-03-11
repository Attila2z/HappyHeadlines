# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy both DraftService and Shared project files
COPY DraftService/DraftService.csproj ./DraftService/
COPY Shared/Shared.csproj ./Shared/

# Restore dependencies for both projects
RUN dotnet restore DraftService/DraftService.csproj

# Copy all source files
COPY DraftService/ ./DraftService/
COPY Shared/ ./Shared/

# Build and publish
WORKDIR /src/DraftService
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "DraftService.dll"]
