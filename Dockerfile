# Stage 1: Build-Umgebung (mit SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Abhängigkeiten kopieren und wiederherstellen
COPY ["src/kDriveWebDav/kDriveWebDav.csproj", "src/kDriveWebDav/"]
RUN dotnet restore "src/kDriveWebDav/kDriveWebDav.csproj"

# Restlichen Code kopieren und bauen
COPY . .
RUN dotnet publish "src/kDriveWebDav/kDriveWebDav.csproj" -c Release -o /app/publish

# Stage 2: Schlanke Laufzeitumgebung (Produktion)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Port freigeben
EXPOSE 8080
ENTRYPOINT ["./kdrive-webdav", "start", "--port", "8080", "--host", "*"]