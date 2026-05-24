FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["shared/SharedMessages/SharedMessages.csproj", "shared/SharedMessages/"]
COPY ["src/InventoryService/InventoryService.csproj", "src/InventoryService/"]
RUN dotnet restore "src/InventoryService/InventoryService.csproj"

COPY . .
RUN dotnet publish "src/InventoryService/InventoryService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryService.dll"]
