FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV WUPM_API_KEY=

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/WupmApi/WupmApi.csproj src/WupmApi/
COPY src/WindowsUpdateAndPackageManager.Core/WindowsUpdateAndPackageManager.csproj src/WindowsUpdateAndPackageManager.Core/
RUN dotnet restore src/WupmApi/WupmApi.csproj
COPY . .
RUN dotnet build src/WupmApi/WupmApi.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish src/WupmApi/WupmApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WupmApi.dll"]
