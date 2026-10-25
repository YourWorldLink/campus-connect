# 图标来源

## 界面图标

全部界面图标取自 [Ant Design Icons](https://github.com/ant-design/ant-design-icons)（npm 包 `@ant-design/icons-svg` 4.6.0，**MIT 许可**），下载自 `https://unpkg.com/@ant-design/icons-svg@4.6.0/inline-svg/outlined/<名称>.svg`。

| 本仓库文件 | 上游图标 | 用途 |
| --- | --- | --- |
| `wifi.path` | `wifi` | 应用标志、连接状态、连接按钮 |
| `settings.path` | `setting` | 设置入口、高级设置 |
| `user.path` | `user` | 账号状态 |
| `clock.path` | `clock-circle` | 时间、运行记录、最近连接 |
| `globe.path` | `global` | 网络类型 |
| `info.path` | `info-circle` | 关于 |
| `save.path` | `save` | 保存配置 |
| `link.path` | `link` | 桌面图标 |
| `eye.path` | `eye` | 显示密码 |
| `eye-off.path` | `eye-invisible` | 隐藏密码 |

只保留 SVG 的路径数据（`d` 属性），颜色与尺寸在运行时决定。上游坐标网格为 `viewBox="64 64 896 896"`，见 `src/Design.cs` 中 `Icons` 类的换算。许可全文与版权声明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

> 早期版本使用 iconfont.cn 上的图标。iconfont 是设计师作品的上传平台，各图标授权由上传者各自设定；其[用户服务协议](https://terms.alicdn.com/legal-agreement/terms/platform_service/20220704165734807/20220704165734807.html)第五条第 2 款明确禁止将站内素材「发布至第三方平台或网站」，因此不适合随源码公开分发，已在全部替换为上述 MIT 图标。

## 软件图标

`.ico` 由 `assets/app-icon.png` 生成：构建时输出 16–256 像素多尺寸 `campus.ico`，用于 EXE、任务栏及桌面快捷方式。

该图片由本仓库作者使用 AI 图像生成工具创作，**不包含任何他人在先作品的复制**，因此不存在第三方版权侵权风险，可以随本仓库一并分发。

需要留意两点：

- 各国对 AI 生成内容的可版权性认定不同。例如美国版权局要求「人类作者」，纯 AI 输出通常不构成可受版权保护的作品；中国已有判例在人类投入足够（提示词设计、参数调整、多轮筛选等）的前提下认可其可版权性。因此本仓库**不对该图片主张独占权利**。
- 生成式模型有可能输出与训练数据中知名标识相似的图形。此处是通用的 Wi-Fi 符号，不含任何品牌标识，无商标风险。

## 程序直接绘制的内容

绿色渐变、圆角窗口与卡片、连接横幅里的笔记本与植物装饰、时间线节点等，均由 `src/Design.cs` 用 GDI+ 绘制，不含第三方素材。
