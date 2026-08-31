FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SecurityCenterAI.Api/SecurityCenterAI.Api.csproj", "src/SecurityCenterAI.Api/"]
COPY ["src/SecurityCenterAI.Domain/SecurityCenterAI.Domain.csproj", "src/SecurityCenterAI.Domain/"]
COPY ["src/SecurityCenterAI.Infrastructure/SecurityCenterAI.Infrastructure.csproj", "src/SecurityCenterAI.Infrastructure/"]
RUN dotnet restore src/SecurityCenterAI.Api/SecurityCenterAI.Api.csproj
COPY src/ src/
RUN dotnet publish src/SecurityCenterAI.Api/SecurityCenterAI.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SecurityCenterAI.Api.dll"]
