FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["FileStorageService/FileStorageService.csproj", "FileStorageService/"]
RUN dotnet restore "FileStorageService/FileStorageService.csproj"
COPY . .
WORKDIR "/src/FileStorageService"
RUN dotnet build "FileStorageService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FileStorageService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FileStorageService.dll"]