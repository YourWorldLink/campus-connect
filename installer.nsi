; 校园网一键连接 —— 安装程序脚本（NSIS 3）
; 由 make-installer.ps1 调用 vendor\nsis\makensis.exe 编译。
; 本文件含中文，必须以 UTF-8 BOM 保存，否则 makensis 会按系统代码页解析而乱码。

Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!ifndef APP_VERSION
  !error "缺少 APP_VERSION：请用 makensis /DAPP_VERSION=x.y 传入。"
!endif
!ifndef APP_VERSION4
  !define APP_VERSION4 "${APP_VERSION}.0.0"
!endif
!ifndef SOURCE_DIR
  !error "缺少 SOURCE_DIR：payload 目录的绝对路径。"
!endif
!ifndef OUT_FILE
  !error "缺少 OUT_FILE：安装包输出路径。"
!endif

!define APP_NAME "校园网一键连接"
!define APP_EXE "CampusConnect.exe"
!define APP_KEY "CampusConnect"
!define APP_PUBLISHER "校园网一键连接"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_KEY}"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define WEBVIEW2_CLIENT "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUT_FILE}"
; 当前用户目录，全程不触发 UAC，与程序「不需要管理员权限」的定位一致。
InstallDir "$LOCALAPPDATA\Programs\CampusConnect"
InstallDirRegKey HKCU "Software\${APP_KEY}" "InstallDir"
RequestExecutionLevel user
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${APP_VERSION4}"
VIAddVersionKey /LANG=2052 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=2052 "FileDescription" "${APP_NAME} 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "安装程序使用 NSIS，界面组件使用 WebView2，各自保留原始许可"

!define MUI_ICON "${SOURCE_DIR}\campus.ico"
!define MUI_UNICON "${SOURCE_DIR}\campus.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "立即运行 ${APP_NAME}"
!define MUI_WELCOMEPAGE_TEXT "即将安装 ${APP_NAME} ${APP_VERSION}。$\r$\n$\r$\n程序会安装到当前用户目录，不需要管理员权限，也不会添加系统服务或计划任务。$\r$\n$\r$\n已保存的校园网账号会自动沿用，无需重新填写。"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
  ; WebView2 Evergreen 运行时：先查按机器安装，再查按用户安装。
  ClearErrors
  ReadRegStr $0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_CLIENT}" "pv"
  ReadRegStr $1 HKCU "Software\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_CLIENT}" "pv"
  StrCmp $0 "" 0 webview_ok
  StrCmp $1 "" 0 webview_ok
    ${IfNot} ${Silent}
      MessageBox MB_YESNO|MB_ICONEXCLAMATION "未检测到 Microsoft Edge WebView2 运行时。$\r$\n$\r$\n${APP_NAME} 需要它才能显示学校的认证页面。$\r$\n$\r$\n是否现在打开微软官方下载页面？" IDNO webview_ok
      ExecShell "open" "https://developer.microsoft.com/microsoft-edge/webview2/#download-section"
    ${EndIf}
  webview_ok:

  ; .NET Framework 4.7.2 对应 Release 461808；Win10 1803 以后系统自带，这里只做提示。
  ClearErrors
  ReadRegDWORD $2 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IfErrors 0 +2
    StrCpy $2 0
  IntCmp $2 461808 netfx_ok netfx_warn netfx_ok
  netfx_warn:
    ${IfNot} ${Silent}
      MessageBox MB_OK|MB_ICONEXCLAMATION "未检测到 .NET Framework 4.7.2 或更高版本。$\r$\n$\r$\n${APP_NAME} 需要它才能运行，请先安装后再使用。"
    ${EndIf}
  netfx_ok:
FunctionEnd

Section "${APP_NAME}" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File /r "${SOURCE_DIR}\*.*"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\卸载 ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

  WriteRegStr HKCU "Software\${APP_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $3 $4 $5
  IntFmt $3 "0x%08X" $3
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize" "$3"
SectionEnd

Section "创建桌面快捷方式" SecDesktop
  ; 程序自己也提供「桌面图标」按钮，两者指向同一个 .lnk，不会冲突。
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "安装 ${APP_NAME} 到当前用户目录，并创建开始菜单快捷方式。"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "在桌面创建快捷方式，开机后双击即可连接校园网。"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\卸载 ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  ; 程序写入的开机自启项（若用户从未开启过，这里删除是空操作）。
  DeleteRegValue HKCU "${RUN_KEY}" "${APP_KEY}"
  DeleteRegKey HKCU "Software\${APP_KEY}"
  DeleteRegKey HKCU "${UNINST_KEY}"

  ; WebView2 的浏览缓存是纯临时数据，直接清理。
  RMDir /r "$LOCALAPPDATA\CampusConnect\Browser"

  ; 账号密码是用户数据，必须先问；静默卸载时一律保留。
  ${If} ${FileExists} "$LOCALAPPDATA\CampusConnect\settings.json"
    IfSilent keep_credentials
    MessageBox MB_YESNO|MB_ICONQUESTION "是否同时删除已保存的校园网账号和密码？$\r$\n$\r$\n保留它们，下次安装后可直接继续使用。" IDNO keep_credentials
    RMDir /r "$LOCALAPPDATA\CampusConnect"
    Goto credentials_done
  keep_credentials:
    DetailPrint "已保留保存的账号和密码。"
  credentials_done:
  ${EndIf}

  RMDir /r "$INSTDIR"
SectionEnd
