# Build a partir da raiz do repositório (context: .), pois Basket.API
# referencia src/BuildingBlocks/EventBus.Messages via ProjectReference.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/Basket.API/Basket.API.csproj", "src/Services/Basket.API/"]
COPY ["src/BuildingBlocks/EventBus.Messages/EventBus.Messages.csproj", "src/BuildingBlocks/EventBus.Messages/"]
RUN dotnet restore "src/Services/Basket.API/Basket.API.csproj"

COPY . .
WORKDIR "/src/src/Services/Basket.API"
RUN dotnet build "Basket.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Basket.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Basket.API.dll"]