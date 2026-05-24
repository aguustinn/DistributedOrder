FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["shared/SharedMessages/SharedMessages.csproj", "shared/SharedMessages/"]
COPY ["src/NotificationService/NotificationService.csproj", "src/NotificationService/"]
RUN dotnet restore "src/NotificationService/NotificationService.csproj"

COPY . .
RUN dotnet publish "src/NotificationService/NotificationService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.dll"]
