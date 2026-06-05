FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 核心修改：拷贝两个项目的 csproj 以进行 restore
COPY ["DZI-api/DZI-api.csproj", "DZI-api/"]
COPY ["DZI-shared/DZI-shared.csproj", "DZI-shared/"]

RUN dotnet restore "./DZI-api/DZI-api.csproj"

# 拷贝全量代码
COPY . .
WORKDIR "/src/DZI-api"
RUN dotnet build "./DZI-api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DZI-api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DZI-api.dll"]
