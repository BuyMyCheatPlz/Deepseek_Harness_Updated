# DeepSeek Harness v__VERSION__ (Windows x64 Setup)

> 🚀 **DeepSeek Harness** Windows 桌面独立安装包，基于本 Fork 独家特性定制构建，开箱即用。

---

## ✨ 核心特性与亮点 (Key Features)

### 1. 🧠 Plan / Act 双模型智能分流 (Cline 风格)
- **Plan 模式 (架构规划)**：自动调度高推理模型（`deepseek-v4-pro` / `deepseek-reasoner`）进行任务深度拆解、架构推演、技术选型与规划思考。
- **Act 模式 (代码执行)**：自动调度高吞吐执行模型（`deepseek-v4-flash` / `deepseek-chat`）高速完成代码编写、文件搜索、编辑替换以及终端命令调用。
- **大幅提速与节省成本**：兼顾超强规划深度与极致执行效率，避免执行阶段消耗昂贵的高阶思考 token。

### 2. 🎛️ 模型选择器 Auto / Manual 开关
- 前端交互界面与模型选择器新增 **Auto / Manual** 切换开关：
  - **Auto 模式**：根据当前会话处于 Plan 还是 Act 阶段，全自动切换最适宜的底层模型。
  - **Manual 模式**：支持开发者自主锁定任意特定模型进行全流程交互，满足自由定制需求。

### 3. 📦 纯净独立，零环境依赖 (Self-Contained Windows Setup)
- **Windows x64 Setup 独立安装包**：提供规范的 Windows 安装向导界面。
- **内置完整运行环境**：自带便携版 Node.js 22 LTS 运行核心与 WebView2 前端容器，**无需** 在宿主机配置 Node.js、pnpm、Python 或 Visual C++ 构建工具链。
- **系统集成**：安装向导自动创建 Windows 开始菜单项、桌面快捷方式，并注册标准卸载程序。

### 4. 🛡️ 构建与会话系统稳定性加固
- 优化了 Windows 安装包打包阶段的 overlay 依赖热补丁机制，支持路径自修复与依赖补齐。
- 完善会话紧凑数据（Session Log）自动迁移与模型路由器运行时不变性（Runtime Invariants）安全校验。

---

## 📥 安装与快速上手 (Installation & Quick Start)

1. **下载安装包**：在下方 **Assets** 列表中下载 `DeepSeek-Harness-Setup-__VERSION__.exe`。
2. **执行安装向导**：双击运行安装程序，按照向导完成安装（支持默认安装至 `%ProgramFiles%\DeepSeek Harness` 或自定义路径）。
3. **启动应用**：从桌面快捷方式或 Windows 开始菜单启动 **DeepSeek Harness**。
4. **配置 API Key**：首次启动时，在设置界面填入您的 DeepSeek API Key（亦支持任何兼容 OpenAI / DeepSeek 协议的代理 Base URL）。

---

## 🗑️ 卸载说明 (Uninstallation)

如需卸载，可通过以下任一方式：
- 打开 Windows「**设置**」→「**应用**」→「**安装的应用 / 应用和功能**」，找到 **DeepSeek Harness** 点击「卸载」。
- 或直接运行安装根目录中的 `unins000.exe` 卸载向导。

---

## 📝 开发者与打包说明 (Notes)

- 上游官方 npm 发布的 `@deepseek-ai/dsh` 暂未包含本 Fork 的 Plan/Act 智能分流与 Auto 开关，本安装包已将所有 Fork 改进完整内嵌。
- 更多 Windows 本地调试与重新打包详情请参阅代码库中的 [`apps/windows/README.md`](https://github.com/BuyMyCheatPlz/Deepseek_Harness_Updated/blob/main/apps/windows/README.md)。
