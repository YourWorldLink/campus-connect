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
            Console.WriteLine("RESULT: " + count + " tests passed");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
    }
}
