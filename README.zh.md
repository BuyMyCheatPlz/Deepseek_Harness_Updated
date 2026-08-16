# DeepSeek Harness

[English](README.md) | 中文

DeepSeek Harness（`dsh`）是由 [DeepSeek AI](https://deepseek.com) 开发的开源 agent harness（智能体框架）。

它采用**一切皆插件**的架构，并由 [Cordis](https://github.com/cordiverse/cordis) 驱动，其设计参见论文 [_A Programming Paradigm for Spatiotemporal Composability_](https://github.com/cordiverse/paper)。

## 开发者预览

DeepSeek Harness 目前处于 _开发者预览_ 阶段，正在快速迭代。**未来将出现破坏兼容性的变更。**

## 运行

### 通过 `npm` 运行

安装 `Node.js`，然后运行：

```sh
npx @deepseek-ai/dsh web
```

该命令会启动 Web UI，默认地址为 `http://127.0.0.1:3080`。详见 [Web UI 指南](docs/user/guide/index.md)。

### 从源码运行

如需从仓库源码运行：

```sh
git clone https://github.com/deepseek-ai/deepseek-harness.git
cd deepseek-harness
pnpm install
pnpm run build
pnpm dsh web
```

### Windows 桌面应用（exe）

仓库提供开箱即用的 Windows 桌面启动器，位于 `apps/windows/build`（双击 `DeepSeek Harness.exe` 即可）。它把 `dsh web` 包装进内嵌的 WebView2 窗口，并内置 Plan/Act 双模型分流，详见 [Windows 应用 README](apps/windows/README.md)。在新电脑上部署和使用：

- 安装 Node.js 22.19 或更高版本（或 24+）；Microsoft Edge WebView2 运行时随 Windows 11 与大多数 Windows 10 自带。
- 把整个 `apps/windows/build` 文件夹（或仓库根目录的 `DeepSeek-Harness-dual-model-router-windows-x64.zip`）拷贝到新电脑，双击 `DeepSeek Harness.exe`。
- 首次启动时，在「设置 → 模型」填入你的 DeepSeek API key——它会被存到 `%USERPROFILE%\.dsh\.credentials.yaml`。
- 使用输入区的 Plan/Act 开关：`Plan` 用 `deepseek-v4-pro`，`Act` 用 `deepseek-v4-flash`。

Windows 启动器的构建、配置、日志与自更新详见 [apps/windows/README.md](apps/windows/README.md)；注意从源码用 `build.ps1 -BundleDsh` 重建会重装上游 npm 的 `@deepseek-ai/dsh`，**必须**再跑一次 `apps/windows/rebundle.ps1` 以重新套用本 fork 的分流改动（见其中的重新打包注意）。

## 社区与支持

- 欢迎通过 [GitHub Discussions](https://github.com/deepseek-ai/deepseek-harness/discussions) 提交反馈或 bug 报告。
- 为你的插件仓库添加 [`dsh-plugin`](https://github.com/topics/dsh-plugin) 话题，便于被发现。
- 欢迎加入 DeepSeek Harness 企微群：扫码添加企微小助手并填写入群问卷，完成后小助手会邀请你入群。

<table>
  <thead>
    <tr>
      <th align="center">企微小助手</th>
      <th align="center">入群问卷</th>
      <th align="center">微信公众号</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td align="center"><img src="assets/community-wecom-assistant.png" alt="DeepSeek Harness 企微小助手二维码" width="180" height="180"></td>
      <td align="center"><a href="https://trtgsjkv6r.feishu.cn/share/base/form/shrcnIt5twSVdLGD52KJBckGCgg"><img src="assets/community-wecom-survey.png" alt="DeepSeek Harness 入群问卷二维码" width="180" height="180"></a></td>
      <td align="center"><img src="assets/community-wechat-official-account.png" alt="DeepSeek Harness 团队微信公众号二维码" width="180" height="180"></td>
    </tr>
  </tbody>
</table>

## 参与贡献

参见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 开发

请先阅读[开发指南](docs/development.md)与[架构文档](docs/architecture.md)。

面向 agent：请遵循 [AGENTS.md](AGENTS.md)。

## 许可证

[MIT](LICENSE)

第三方依赖及其许可证见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
