using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CampusConnect;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

sealed class BrowserTests : Form
{
    readonly WebView2 browser = new WebView2 { Dock = DockStyle.Fill };
    readonly List<string> results = new List<string>();
    readonly string output;
    int failures;
    string fixtureHtml;
    BrowserTests(string output)
    {
        this.output = output;
        Text = "校园网连接工具 · 本地认证测试";
        Size = new Size(640, 400);
        Controls.Add(browser);
        Shown += async (sender, e) => await RunAsync();
    }
    void Check(bool condition, string name)
    {
        results.Add((condition ? "PASS " : "FAIL ") + name);
        if (!condition) failures++;
    }
    async Task Page(string body)
    {
        var completion = new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = (sender, e) => completion.TrySetResult(e.IsSuccess);
        browser.CoreWebView2.NavigationCompleted += handler;
        try
        {
            fixtureHtml = "<!doctype html><html><meta charset='utf-8'><body>" + body + "</body></html>";
            browser.CoreWebView2.Navigate("http://campus-fixture.test/page/" + Guid.NewGuid().ToString("N"));
            if (await Task.WhenAny(completion.Task, Task.Delay(8000)) != completion.Task) throw new Exception("fixture navigation timeout");
            if (!await completion.Task) throw new Exception("fixture navigation failed");
        }
        finally { browser.CoreWebView2.NavigationCompleted -= handler; }
    }
    async Task<PortalReport> Run(Settings settings, bool submit = true)
    {
        return Json.Read<PortalReport>(await browser.ExecuteScriptAsync(PortalScript.Build(settings, submit)));
    }
    async Task RunAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetDirectoryName(output), "test-webview"));
            var options = env.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled = true;
            await browser.EnsureCoreWebView2Async(env, options);
            browser.CoreWebView2.AddWebResourceRequestedFilter("http://campus-fixture.test/*", CoreWebView2WebResourceContext.All);
            browser.CoreWebView2.WebResourceRequested += (sender, e) =>
                e.Response = env.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(fixtureHtml ?? "")), 200, "OK", "Content-Type: text/html; charset=utf-8");
            var settings = new Settings { PortalUrl = "http://campus-fixture.test/", Username = "test-student", Password = "quote\"\\秘密<script>" };
            string form = "<input id='username'><input id='password' type='password'><label><input id='agree' type='checkbox'>我已阅读并同意服务协议</label><button id='login'>登录</button>" +
                "<script>window.clicks=0;window.events=0;username.addEventListener('input',()=>window.events++);password.addEventListener('change',()=>window.events++);login.onclick=()=>window.clicks++;</script>";
            await Page(form);
            Check((await Run(settings)).State == "submitted", "SRun form submits");
            Check(await browser.ExecuteScriptAsync("document.getElementById('username').value") == Json.Write(settings.Username), "account filled");
            // Compare inside the browser to avoid depending on Unicode escaping conventions.
            Check(await browser.ExecuteScriptAsync("document.getElementById('password').value === " + Json.Write(settings.Password)) == "true", "special-character password stays literal");
            Check(await browser.ExecuteScriptAsync("window.events === 2 && window.clicks === 1 && document.getElementById('agree').checked") == "true", "input events, agreement, single submission");
            await Run(settings, false);
            Check(await browser.ExecuteScriptAsync("window.clicks === 1") == "true", "polling never resubmits password");

            await Page(form + "<input placeholder='请输入验证码' name='captcha'>");
            Check((await Run(settings)).State == "manual", "captcha requests user action");
            Check(await browser.ExecuteScriptAsync("window.clicks === 0") == "true", "captcha prevents unattended submission");
            await Page("<p>页面仍在加载</p>");
            Check((await Run(settings)).State == "missing", "missing form handled");
            await Page("<button>注销</button><p>在线信息</p>");
            Check((await Run(settings)).State == "portal-online", "already-online page detected");
            await Page(form + "<div class='layui-layer-content'>用户名或密码错误</div>");
            Check((await Run(settings)).State == "error", "SRun visible authentication error detected");
            await Page(form + "<div class='layui-layer-content' style='display:none'>密码错误</div>");
            Check((await Run(settings)).State == "submitted", "hidden error template ignored");

            settings.UsernameSelector = "#custom-user";
            settings.PasswordSelector = "#custom-pass";
            settings.LoginSelector = "#custom-button";
            await Page("<input id='custom-user'><input id='custom-pass' type='password'><button id='custom-button' onclick='window.sent=true'>继续</button>");
            Check((await Run(settings)).State == "submitted", "custom selectors supported");
            Check(await browser.ExecuteScriptAsync("window.sent === true") == "true", "custom submit clicked");
            settings.UsernameSelector = "[";
            Check((await Run(settings)).State == "manual", "invalid CSS selector handled");
            settings.UsernameSelector = settings.PasswordSelector = settings.LoginSelector = "";

            await Page("<iframe id='form-frame' srcdoc=\"<input id='username'><input type='password' id='password'><button id='login'>登录</button>\"></iframe>");
            Check((await Run(settings)).State == "submitted", "same-origin iframe form supported");
            Check(await browser.ExecuteScriptAsync("document.querySelector('iframe').contentDocument.querySelector('#username').value === " + Json.Write(settings.Username)) == "true", "iframe account filled");
            await Page("<iframe sandbox='allow-scripts' srcdoc=\"<input id='username'><input type='password' id='password'><button id='login'>登录</button>\"></iframe>");
            Check((await Run(settings)).State == "missing", "cross-origin iframe cannot receive credentials");
            await Page(form);
            settings.PortalUrl = "http://untrusted-fixture.test/";
            Check((await Run(settings)).State == "redirect", "in-page origin check blocks navigation race");
            Check(await browser.ExecuteScriptAsync("document.getElementById('password').value === '' && window.clicks === 0") == "true", "untrusted page never receives password");
        }
        catch (Exception ex) { failures++; results.Add("FAIL " + ex.ToString()); }
        finally
        {
            results.Add("RESULT: " + (results.Count - failures) + " passed, " + failures + " failed");
            File.WriteAllLines(output, results);
            Environment.ExitCode = failures == 0 ? 0 : 1;
            browser.Dispose();
            Close();
        }
    }
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new BrowserTests(Path.GetFullPath(args[0])));
    }
}
