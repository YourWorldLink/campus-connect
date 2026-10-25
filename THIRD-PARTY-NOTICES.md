# 第三方组件与许可

本项目自身源码的许可由作者另行声明。下列第三方内容随本项目一起分发，各自保留原始许可。

## Ant Design Icons（界面图标）

`assets/icons/*.path` 取自 [Ant Design Icons](https://github.com/ant-design/ant-design-icons)，版本 `@ant-design/icons-svg` 4.6.0，MIT 许可。

- 图标清单（本仓库名 ← 上游名）：`wifi` ← wifi，`settings` ← setting，`user` ← user，`clock` ← clock-circle，`globe` ← global，`info` ← info-circle，`save` ← save，`link` ← link，`eye` ← eye，`eye-off` ← eye-invisible
- 上游坐标网格为 `viewBox="64 64 896 896"`；本项目只保留路径数据，颜色与尺寸在运行时决定。

```
MIT LICENSE

Copyright (c) 2018-present Ant UED, https://xtech.antfin.com/

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

## Microsoft Edge WebView2 SDK

分发目录中的 `Microsoft.Web.WebView2.Core.dll`、`Microsoft.Web.WebView2.WinForms.dll`、`WebView2Loader.dll` 来自 Microsoft WebView2 SDK 1.0.2903.40，随分发目录附 `WebView2-LICENSE.txt`：

```
Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * The name of Microsoft Corporation, or the names of its contributors
may not be used to endorse or promote products derived from this
software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

WebView2 **运行时**由用户机器上的 Microsoft Edge WebView2 Evergreen Runtime 提供，本仓库不再分发。

## NSIS（仅构建工具，不随程序分发）

`make-installer.ps1` 使用 NSIS 3 编译安装包。NSIS 采用 zlib/libpng 许可，许可全文见 `vendor/nsis/COPYING`（`vendor/` 不纳入版本管理，获取方式见 `vendor/nsis/SOURCE.txt` 与 README）。由 NSIS 生成的安装程序不受其许可约束。

## 微软、深澜等名称

"Microsoft"、"Edge"、"WebView2" 为微软公司商标；"深澜"/"SRun" 为其权利人商标。本文件仅作事实性描述，不表示任何形式的关联、赞助或背书。
