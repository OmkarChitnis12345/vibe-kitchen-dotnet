# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["VibeKitchen.Api.csproj", "./"]
RUN dotnet restore "VibeKitchen.Api.csproj"
COPY . .
RUN dotnet publish "VibeKitchen.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Run the application
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "VibeKitchen.Api.dll"]