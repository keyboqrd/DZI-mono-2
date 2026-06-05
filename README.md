# DZI Image Processing Pipeline

一个基于 .NET 10 的高性能大图切片处理系统。支持将数亿像素的图片（JPG/PNG/TIFF）自动转换为 DeepZoom (DZI) 格式，并存储于 Cloudflare R2。

## 核心架构
*   **DZI-api**: 提供外部接口，负责订阅验证、元数据管理及触发处理任务。
*   **DZI-gen**: 这是一个 Azure Container App Job，按需启动，利用 libvips 进行极速切片，完成后自动销毁。
*   **DZI-shared**: 共享的数据模型与数据库上下文。

---

## 部署与配置指南

本系统依赖环境变量进行配置，建议将这些配置项存入 **Azure App Configuration** 或直接在 Container App 的 **Configuration** 页面设置。

### 1. DZI-api 配置项 (Container App)

| 类别 | 变量名 | 说明 |
| :--- | :--- | :--- |
| **数据库** | `ConnectionStrings__DefaultConnection` | Azure SQL Server 连接字符串 |
| **订阅** | `RevenueCat__ApiKey` | RevenueCat v2 **Secret API Key** |
| | `RevenueCat__ProjectId` | RevenueCat 项目 ID |
| **任务触发** | `AzureContainerApps__SubscriptionId` | Azure 订阅 ID |
| | `AzureContainerApps__ResourceGroupName` | 资源组名称 |
| | `AzureContainerApps__JobName` | Container App Job 的名称 (例如 `dzi-gen`) |
| **内部安全** | `InternalApi__ApiKey` | 自定义字符串，需与 Job 端的配置一致 |
| **存储** | `CloudflareR2__AccountId` | Cloudflare 账户 ID |
| | `CloudflareR2__AccessKeyId` | R2 API Token 的 Access Key |
| | `CloudflareR2__SecretAccessKey` | R2 API Token 的 Secret Key |
| | `CloudflareR2__BucketName` | R2 存储桶名称 |

### 2. DZI-gen 配置项 (Container App Job)

| 类别 | 变量名 | 说明 |
| :--- | :--- | :--- |
| **内部通信** | `InternalApi__ApiKey` | 必须与 API 端的 Key 保持一致 |
| | `InternalApi__BaseUrl` | API 的地址 (默认 `http://dzi-api`，无需外网地址) |
| **存储** | `CloudflareR2__AccountId` | 同上 |
| | `CloudflareR2__AccessKeyId` | 同上 |
| | `CloudflareR2__SecretAccessKey` | 同上 |
| | `CloudflareR2__BucketName` | 同上 |
| **可选** | `Tiling__TileSize` | 切片大小，默认 `1024` |

---

## 关键权限设置 (重要)

为了让 API 有权启动 Job，必须在 Azure Portal 中进行一次性授权：
1.  进入 **Container App Job (dzi-gen)** 页面。
2.  点击 **Access Control (IAM)** -> **Add role assignment**。
3.  分配 **Contributor** (参与者) 角色给 **DZI-api** 的受管理标识 (Managed Identity)。

---

## 如何进行测试

项目内置了两个基于浏览器的测试工具，位于 `/tests` 目录下：

1.  **复制配置**：将 `tests/config.json.example` 复制并重命名为 `tests/config.json`。
2.  **填入参数**：修改其中的 `apiUrl` (Azure API 地址) 和 `r2PublicUrl` (R2 公网访问地址)。
3.  **运行上传测试**：打开 `test-client.html`，选择一张大图上传，观察全自动流程（API -> R2 -> Job -> DB）。
4.  **运行查看测试**：打开 `test-viewer.html`，输入用户 ID 即可列出并交互式查看已完成的超高清大图。

---

## 运维提醒
*   **临时存储**：ACA Job 默认提供 2GB 临时磁盘空间。若原图超过 500MB，建议在 Job 配置中挂载 Azure Files。
*   **数据库初始化**：API 启动时会自动尝试创建数据库表，请确保数据库用户具有 `db_owner` 权限。
