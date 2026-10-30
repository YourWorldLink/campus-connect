# 校园网一键连接

Windows 校园网连接工具，支持连接已保存的 Wi-Fi，并自动填写深澜 SRun 认证页面。

![软件界面](docs/screenshot.png)

## 功能

- 一键连接校园 Wi-Fi 并完成网页认证，展示连接状态和运行记录。
- 搜索附近 Wi-Fi，查看信号和保存状态。
- 支持开机自动连接、系统托盘和断线后自动重连。
- 在设置中切换浅色或深色主题，查看本地日志和配置认证选项。

断线重连仅在仍连接指定校园 Wi-Fi 或没有其他网络连接时进行；连接到其他网络时不会自动切回。

## 使用

1. 在 Windows 中手动连接一次校园 Wi-Fi 并保存密码。
2. 打开程序，填写 Wi-Fi 名称、学校认证网址、校园网账号和密码。
3. 点击“连接网络”；需要开机自动连接时，勾选“开机时自动启动本工具”。

Wi-Fi 名称留空时使用当前网络，也可用于有线网络。认证页面出现验证码或需要手动选择时，请在程序的“认证页面”中完成操作。部分系统需要开启位置权限才能搜索附近 Wi-Fi。

## 系统要求

Windows 10 或 11（64 位）、.NET Framework 4.7.2 或更高版本，以及 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)。程序无需管理员权限。

## 数据与隐私

账号配置和运行日志保存在 `%LOCALAPPDATA%\CampusConnect`。密码使用 Windows 当前用户的 DPAPI 加密保存，不写入日志。README 截图使用固定示例信息，不读取本机账号和认证网址。

## 构建

将 WebView2 SDK 1.0.2903.40 解压到 `vendor/webview2/`，然后在项目目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果位于 `dist/`，脚本会运行核心及渲染测试并更新 README 截图。需要安装包时，依次运行 `package.ps1` 和 `make-installer.ps1`；安装包构建还需要将 NSIS 放在 `vendor/nsis/`。

版本号由根目录的 `VERSION` 文件管理。第三方组件和图标的许可见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 和 [ICON-SOURCES.md](ICON-SOURCES.md)。本仓库源码尚未附许可证。
