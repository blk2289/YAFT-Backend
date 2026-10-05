# Vedere https://aka.ms/customizecontainer per informazioni su come personalizzare il contenitore di debug e su come Visual Studio usa questo Dockerfile per compilare le immagini per un debug più rapido.

# Questa fase viene usata durante l'esecuzione da Visual Studio in modalità rapida (impostazione predefinita per la configurazione di debug)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# Questa fase viene usata per compilare il progetto di servizio (contesto di build: cartella della solution)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["YAFT-Backend/YAFT-Backend.csproj", "YAFT-Backend/"]
COPY ["YAFT-Backend.ServiceDefaults/YAFT-Backend.ServiceDefaults.csproj", "YAFT-Backend.ServiceDefaults/"]
COPY ["src/YAFT.Domain/YAFT.Domain.csproj", "src/YAFT.Domain/"]
COPY ["src/YAFT.Application/YAFT.Application.csproj", "src/YAFT.Application/"]
COPY ["src/YAFT.Infrastructure/YAFT.Infrastructure.csproj", "src/YAFT.Infrastructure/"]
RUN dotnet restore "./YAFT-Backend/YAFT-Backend.csproj"
COPY . .
WORKDIR "/src/YAFT-Backend"
RUN dotnet build "./YAFT-Backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Questa fase viene usata per pubblicare il progetto di servizio da copiare nella fase finale
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./YAFT-Backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Questa fase viene usata nell'ambiente di produzione o durante l'esecuzione da Visual Studio in modalità normale
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YAFT-Backend.dll"]
