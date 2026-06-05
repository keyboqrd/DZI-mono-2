# 构建阶段
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# 安装 Native AOT 编译所需的依赖以及 libvips 编译环境
RUN apt-get update && apt-get install -y --no-install-recommends \
    clang zlib1g-dev libvips-dev \
    && rm -rf /var/lib/apt/lists/*

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 拷贝项目文件进行 restore
COPY ["DZI-gen/DZI-gen.csproj", "DZI-gen/"]
COPY ["DZI-shared/DZI-shared.csproj", "DZI-shared/"]

RUN dotnet restore "./DZI-gen/DZI-gen.csproj"

# 拷贝全量源代码
COPY . .
WORKDIR "/src/DZI-gen"

# 执行发布 (非 AOT 模式)
RUN dotnet publish "./DZI-gen.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# 最终运行阶段 (使用标准 aspnet 运行时)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# 重要：在最终镜像中安装 libvips 运行时库
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    libvips \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DZI-gen.dll"]
