using System;
using System.IO;
using System.Linq;
using System.Text;
using CampusConnect;

static class CoreTests
{
    static int count;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        count++;
        Console.WriteLine("PASS " + name);
    }
    static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, name);
    }
    static int Main()
    {
        try
        {
            var screenshotExample = Settings.CreateScreenshotExample();
            Check(screenshotExample.Ssid == "校园 Wi-Fi" && screenshotExample.PortalUrl == "https://example.com" &&
                screenshotExample.Username == "校园网账号" && screenshotExample.Password == "示例密码", "screenshots use fixed example credentials");
            Check(Validation.Portal("http://192.0.2.10/srun_portal_pc?ac_id=1&theme=pro").Host == "192.0.2.10", "SRun URL preserved");
            Reject(() => Validation.Portal("javascript:alert(1)"), "reject script URL");
            Reject(() => Validation.Portal("file:///C:/secret"), "reject local-file URL");
            Reject(() => Validation.Portal("https://user:pass@example.com"), "reject embedded credentials");
            Reject(() => Validation.Portal("192.0.2.10"), "require absolute URL");
            var origin = Validation.Portal("http://192.0.2.10/login");
            Check(Validation.SameOrigin(origin, "http://192.0.2.10/another?ac_id=1"), "allow same-origin navigation");
            Check(!Validation.SameOrigin(origin, "http://192.0.2.10.evil.test/"), "block lookalike host");
            Check(!Validation.SameOrigin(origin, "http://192.0.2.10:8000/"), "block changed port");
            Check(!Validation.SameOrigin(origin, "https://192.0.2.10/"), "require configured scheme");
            Check(!Validation.SameOrigin(origin, "about:blank"), "block untrusted browser origins");
            var settings = new Settings { Username = "test-student", Password = "秘密\"\\\n<script>not-code</script>" };
            var encrypted = SettingsStore.Protect(settings.Password);
            Check(encrypted != settings.Password && !encrypted.Contains("秘密"), "DPAPI ciphertext");
            Check(SettingsStore.Unprotect(encrypted) == settings.Password, "DPAPI decrypt round trip");
            string path = Path.GetFullPath(Path.Combine("artifacts", "test-settings.json"));
            SettingsStore.Save(settings, path);
            string saved = File.ReadAllText(path);
            Check(!saved.Contains("not-code") && !saved.Contains("秘密"), "plaintext password absent on disk");
            var loaded = Json.Read<Settings>(saved);
            Check(SettingsStore.Unprotect(loaded.EncryptedPassword) == settings.Password, "saved credentials decrypt");
            settings.Username = "updated";
            SettingsStore.Save(settings, path);
            Check(Json.Read<Settings>(File.ReadAllText(path)).Username == "updated", "atomic config replacement");
            Check(!Directory.GetFiles(Path.GetDirectoryName(path), "test-settings.json.*.tmp").Any(), "no temporary secret files");
            Check(Validation.QuoteArgument("School WiFi") == "\"School WiFi\"", "quote SSID with spaces");
            Check(Validation.QuoteArgument("school\"wifi") == "\"school\\\"wifi\"", "escape quote in SSID");
            Check(Validation.QuoteArgument("wifi\\") == "\"wifi\\\\\"", "escape trailing backslash");
            settings.Ssid = new string('a', 33);
            Reject(() => Validation.Check(settings, true), "reject oversized SSID");
            settings.Ssid = "校园网络校园网络校园网络";
            Reject(() => Validation.Check(settings, true), "measure SSID in UTF-8 bytes");
            settings.Ssid = "School\nWiFi";
            Reject(() => Validation.Check(settings, true), "reject control characters in SSID");
            settings.Ssid = "";
            settings.Password = "";
            Reject(() => Validation.Check(settings, true), "require password before connection");
            Validation.Check(settings, false);
            Check(true, "allow incomplete configuration save");
            settings.Password = "quote\"and\\slash";
            string script = PortalScript.Build(settings, true);
            Check(script.StartsWith("(function") && script.Contains("\\\""), "safely serialize script parameters");
            // 开机重试节奏：5、10、20、40、60 之后保持 60 秒，且总时长有上限。
            Check(RetryPolicy.DelaySeconds(1) == 5, "retry backoff starts at 5s");
            Check(RetryPolicy.DelaySeconds(2) == 10, "retry backoff doubles");
            Check(RetryPolicy.DelaySeconds(4) == 40, "retry backoff keeps doubling");
            Check(RetryPolicy.DelaySeconds(5) == 60, "retry backoff caps at 60s");
            Check(RetryPolicy.DelaySeconds(50) == 60, "retry backoff stays capped");
            Reject(() => RetryPolicy.DelaySeconds(0), "reject attempt below 1");
            Check(!RetryPolicy.Expired(TimeSpan.FromMinutes(29)), "retry allowed before limit");
            Check(RetryPolicy.Expired(TimeSpan.FromMinutes(31)), "retry stops after limit");
            Check(RetryPolicy.WaitForStartupWifi(TimeSpan.FromSeconds(30)), "startup retries Wi-Fi profile while service starts");
            Check(!RetryPolicy.WaitForStartupWifi(TimeSpan.FromMinutes(2)), "missing Wi-Fi profile eventually requires attention");
            Check(LaunchPolicy.ShouldConnect(false, true, false, true), "startup connects even when normal auto-connect is disabled");
            Check(!LaunchPolicy.ShouldConnect(false, false, false, true), "normal launch respects auto-connect setting");
            Check(LaunchPolicy.ShouldConnect(false, false, true, true), "normal launch connects when enabled");
            Check(!LaunchPolicy.ShouldConnect(false, true, true, false), "startup requires credentials");
            Check(!LaunchPolicy.ShouldConnect(true, true, true, true), "preview never connects");
            Check(!MonitorPolicy.ShouldReconnect(true, true, NetworkScope.Campus, 1), "one failed probe does not reconnect");
            Check(MonitorPolicy.ShouldReconnect(true, true, NetworkScope.Campus, 2), "two failed probes trigger reconnect on target network");
            Check(MonitorPolicy.ShouldReconnect(true, true, NetworkScope.Disconnected, 2), "fully disconnected campus network can reconnect");
            Check(!MonitorPolicy.ShouldReconnect(true, true, NetworkScope.Other, 2), "other network prevents background reconnect");
            Check(!MonitorPolicy.ShouldReconnect(true, true, NetworkScope.Unknown, 2), "unknown network state prevents background reconnect");
            Check(!MonitorPolicy.ShouldReconnect(false, true, NetworkScope.Campus, 2), "disabled monitoring never reconnects");
            Check(!MonitorPolicy.ShouldReconnect(true, false, NetworkScope.Campus, 2), "paused monitoring never reconnects");
            var availableNetworkType = typeof(Wifi).GetNestedType("AvailableNetwork", System.Reflection.BindingFlags.NonPublic);
            Check(System.Runtime.InteropServices.Marshal.SizeOf(availableNetworkType) == 628, "Windows Wi-Fi network structure layout");
            string logPath = Path.Combine("artifacts", "test-log-" + Guid.NewGuid().ToString("N") + ".txt");
            Check(LogStore.Read(logPath) == "", "missing log is empty");
            LogStore.Append("first\nsecond", logPath);
            Check(LogStore.Read(logPath).Contains("first second"), "log flattens line breaks");
            LogStore.Clear(logPath);
            Check(LogStore.Read(logPath) == "", "log can be cleared");
            var oldSettings = Json.Read<Settings>("{\"Version\":1}");
            Check(oldSettings.Appearance != "dark", "old settings default to light appearance");
            Console.WriteLine("RESULT: " + count + " tests passed");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
    }
}
