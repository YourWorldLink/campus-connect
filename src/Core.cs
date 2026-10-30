using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace CampusConnect
{
    [DataContract]
    public sealed class Settings
    {
        [DataMember] public int Version = 1;
        [DataMember] public string Ssid = "";
        [DataMember] public string ProfileName = "";
        // 预填的是示例地址：保留深澜 SRun 的路径格式，但主机名为占位值，
        // 避免把作者所在学校的认证服务器地址随源码公开。首次使用需改成实际地址。
        [DataMember] public string PortalUrl = "http://portal.example.edu.cn/srun_portal_pc?ac_id=1&theme=pro";
        [DataMember] public string Username = "";
        [DataMember] public string EncryptedPassword = "";
        [DataMember] public bool AutoConnect = true;
        [DataMember] public string LastConnected = "";
        [DataMember] public string UsernameSelector = "";
        [DataMember] public string PasswordSelector = "";
        [DataMember] public string LoginSelector = "";
        [DataMember] public string Appearance = "light";
        [DataMember] public bool MonitorConnection;
        [DataMember] public string Provider = "";
        public string Password { get; set; }
        public bool HasCredentials { get { return !String.IsNullOrWhiteSpace(Username) && !String.IsNullOrEmpty(Password); } }
        public static Settings CreateScreenshotExample()
        {
            return new Settings { Ssid = "校园 Wi-Fi", PortalUrl = "https://example.com",
                Username = "校园网账号", Password = "示例密码", AutoConnect = true };
        }
    }

    public static class LogStore
    {
        public static readonly string FilePath = Path.Combine(SettingsStore.DirectoryPath, "activity.log");
        const long MaxBytes = 512 * 1024;
        public static void Append(string message, string path = null)
        {
            path = path ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                File.WriteAllText(path, "", new UTF8Encoding(false));
            File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message.Replace('\r', ' ').Replace('\n', ' ') + Environment.NewLine, new UTF8Encoding(false));
        }
        public static string Read(string path = null)
        {
            path = path ?? FilePath;
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
        }
        public static void Clear(string path = null)
        {
            path = path ?? FilePath;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static class Json
    {
        public static string Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static T Read<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
    }

    public static class SettingsStore
    {
        public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CampusConnect");
        public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
        public static string Protect(string password)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser));
        }
        public static string Unprotect(string encrypted)
        {
            if (String.IsNullOrEmpty(encrypted)) return "";
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser));
        }
        public static Settings Load()
        {
            if (!File.Exists(FilePath)) return new Settings();
            var settings = Json.Read<Settings>(File.ReadAllText(FilePath, Encoding.UTF8));
            if (settings.Version != 1) throw new InvalidDataException("配置文件版本不兼容。");
            settings.Password = Unprotect(settings.EncryptedPassword);
            return settings;
        }
        public static void Save(Settings settings, string destination = null)
        {
            settings.EncryptedPassword = Protect(settings.Password ?? "");
            var path = destination ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Json.Write(settings), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public static class Validation
    {
        public static Uri Portal(string input)
        {
            Uri uri;
            if (!Uri.TryCreate(input, UriKind.Absolute, out uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") || String.IsNullOrEmpty(uri.Host) || !String.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("请输入完整的 http:// 或 https:// 校园网认证网址。");
            return uri;
        }
        public static bool SameOrigin(Uri configured, string actual)
        {
            Uri current;
            return Uri.TryCreate(actual, UriKind.Absolute, out current) &&
                String.Equals(configured.Scheme, current.Scheme, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(configured.Host, current.Host, StringComparison.OrdinalIgnoreCase) && configured.Port == current.Port;
        }
        public static void Check(Settings settings, bool connecting)
        {
            Portal(settings.PortalUrl);
            if (Encoding.UTF8.GetByteCount(settings.Ssid ?? "") > 32 || (settings.Ssid ?? "").IndexOfAny(new char[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("SSID 不能超过 32 个 UTF-8 字节，也不能包含换行。");
            if ((settings.ProfileName ?? "").IndexOfAny(new char[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Wi-Fi 配置名称不能包含换行。");
            if (connecting && !settings.HasCredentials) throw new ArgumentException("请先填写校园网账号和密码。");
        }
        public static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') result.Append('\\', slashes * 2 + 1).Append('"');
                else result.Append('\\', slashes).Append(character);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }

    // 开机自启时的重试节奏：5、10、20、40、60、60… 秒，并设总时长上限。
    // 抽成纯函数是为了能直接用 CoreTests 覆盖，不必真的把网络弄断。
    public static class RetryPolicy
    {
        public static readonly TimeSpan Limit = TimeSpan.FromMinutes(30);
        public static int DelaySeconds(int attempt)
        {
            if (attempt < 1) throw new ArgumentOutOfRangeException("attempt");
            return Math.Min(5 << Math.Min(attempt - 1, 4), 60);
        }
        public static bool Expired(TimeSpan elapsed) { return elapsed > Limit; }
    }

    public static class LaunchPolicy
    {
        public static bool ShouldConnect(bool preview, bool autostart, bool autoConnect, bool hasCredentials)
        {
            return !preview && hasCredentials && (autostart || autoConnect);
        }
    }

    public static class MonitorPolicy
    {
        public static bool ShouldReconnect(bool enabled, bool armed, NetworkScope scope, int consecutiveFailures)
        {
            return enabled && armed && (scope == NetworkScope.Campus || scope == NetworkScope.Disconnected) && consecutiveFailures >= 2;
        }
    }

    public enum NetworkScope { Campus, Disconnected, Other, Unknown }

    public static class NetworkContext
    {
        public static NetworkScope GetScope(string target)
        {
            try
            {
                List<string> ssids;
                if (String.IsNullOrWhiteSpace(target) || !Wifi.TryConnectedSsids(out ssids)) return NetworkScope.Unknown;
                if (ssids.Exists(s => !String.Equals(s, target, StringComparison.Ordinal))) return NetworkScope.Other;
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        if (ssids.Count == 0) return NetworkScope.Unknown;
                        continue;
                    }
                    foreach (var address in adapter.GetIPProperties().UnicastAddresses)
                    {
                        var ip = address.Address;
                        if (System.Net.IPAddress.IsLoopback(ip)) continue;
                        if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("169.254.")) return NetworkScope.Other;
                        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !ip.IsIPv6LinkLocal && !ip.IsIPv6Multicast) return NetworkScope.Other;
                    }
                }
                return ssids.Contains(target) ? NetworkScope.Campus : NetworkScope.Disconnected;
            }
            catch (Exception) { return NetworkScope.Unknown; }
        }
    }

    // The per-user Run key is the single source of truth: writing it needs no
    // elevation, and the UI reads it back rather than trusting settings.json.
    public static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "CampusConnect";
        static string Executable()
        {
            using (var process = Process.GetCurrentProcess()) return process.MainModule.FileName;
        }
        // --autostart lets the app give the network time to come up first.
        static string Command() { return Validation.QuoteArgument(Executable()) + " --autostart"; }
        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                return key != null && key.GetValue(ValueName) != null;
        }
        // Enabled *and* still pointing at this executable. Upgrading into a new folder
        // would otherwise leave a stale path that stops launching the app entirely.
        public static bool IsCurrent()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                if (key == null) return false;
                return String.Equals(key.GetValue(ValueName) as string, Command(), StringComparison.OrdinalIgnoreCase);
            }
        }
        public static void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) throw new InvalidOperationException("无法写入 Windows 启动项，未能修改开机自启动设置。");
                if (enabled) key.SetValue(ValueName, Command(), RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
        }
    }

    public static class Wifi
    {
        [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr client);
        [DllImport("wlanapi.dll")] static extern uint WlanEnumInterfaces(IntPtr client, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern uint WlanQueryInterface(IntPtr client, ref Guid id, int opcode, IntPtr reserved, out uint size, out IntPtr data, out int type);
        [DllImport("wlanapi.dll")] static extern uint WlanScan(IntPtr client, ref Guid id, IntPtr ssid, IntPtr ieData, IntPtr reserved);
        [DllImport("wlanapi.dll")] static extern uint WlanGetAvailableNetworkList(IntPtr client, ref Guid id, uint flags, IntPtr reserved, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr data);
        [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr client, IntPtr reserved);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct AvailableNetwork
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile;
            public uint SsidLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Ssid;
            public uint BssType, BssidCount, Connectable, Reason, PhyCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] PhyTypes;
            public uint MorePhy, Signal, Security, Authentication, Cipher, Flags, Reserved;
        }
        public sealed class NearbyNetwork
        {
            public string Ssid, Profile;
            public uint Signal;
            public bool Connected, Secured;
        }
        public static List<NearbyNetwork> ScanNearby()
        {
            uint version; IntPtr client;
            uint error = WlanOpenHandle(2, IntPtr.Zero, out version, out client);
            if (error != 0) throw new InvalidOperationException("无法访问无线网络服务（错误 " + error + "）。请检查 Wi-Fi 和 WLAN 自动配置服务。");
            IntPtr interfaces = IntPtr.Zero;
            try
            {
                error = WlanEnumInterfaces(client, IntPtr.Zero, out interfaces);
                if (error != 0) throw new InvalidOperationException("无法读取无线网卡（错误 " + error + "）。");
                int count = Marshal.ReadInt32(interfaces);
                if (count == 0) throw new InvalidOperationException("没有找到无线网卡，请检查 Wi-Fi 是否已开启。");
                var ids = new List<Guid>();
                for (int i = 0; i < count; i++)
                {
                    var id = (Guid)Marshal.PtrToStructure(IntPtr.Add(interfaces, 8 + i * 532), typeof(Guid));
                    error = WlanScan(client, ref id, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    if (error == 5) throw new InvalidOperationException("Windows 拒绝了 Wi-Fi 扫描。请检查位置服务及桌面应用的位置权限。");
                    if (error != 0) throw new InvalidOperationException("无法扫描附近网络（错误 " + error + "）。请检查 Wi-Fi 是否开启。");
                    ids.Add(id);
                }
                Thread.Sleep(4000);
                var found = new Dictionary<string, NearbyNetwork>(StringComparer.Ordinal);
                foreach (var interfaceId in ids)
                {
                    var id = interfaceId; IntPtr list = IntPtr.Zero;
                    error = WlanGetAvailableNetworkList(client, ref id, 0, IntPtr.Zero, out list);
                    if (error != 0) throw new InvalidOperationException(error == 5 ? "Windows 拒绝读取 Wi-Fi 列表。请检查位置服务及桌面应用的位置权限。" : "无法读取附近网络（错误 " + error + "）。");
                    try
                    {
                        int entries = Marshal.ReadInt32(list);
                        int size = Marshal.SizeOf(typeof(AvailableNetwork));
                        for (int i = 0; i < entries; i++)
                        {
                            var item = (AvailableNetwork)Marshal.PtrToStructure(IntPtr.Add(list, 8 + i * size), typeof(AvailableNetwork));
                            if (item.SsidLength == 0 || item.SsidLength > 32) continue;
                            string name = Encoding.UTF8.GetString(item.Ssid, 0, (int)item.SsidLength);
                            var candidate = new NearbyNetwork { Ssid = name, Profile = item.Profile, Signal = item.Signal, Connected = (item.Flags & 1) != 0, Secured = item.Security != 0 };
                            NearbyNetwork previous;
                            if (!found.TryGetValue(name, out previous) || (candidate.Connected && !previous.Connected) || (!candidate.Connected && !previous.Connected && !String.IsNullOrEmpty(candidate.Profile) && String.IsNullOrEmpty(previous.Profile)) || (candidate.Connected == previous.Connected && String.IsNullOrEmpty(candidate.Profile) == String.IsNullOrEmpty(previous.Profile) && candidate.Signal > previous.Signal)) found[name] = candidate;
                        }
                    }
                    finally { if (list != IntPtr.Zero) WlanFreeMemory(list); }
                }
                var result = new List<NearbyNetwork>(found.Values);
                result.Sort((a, b) => { int connected = b.Connected.CompareTo(a.Connected); return connected != 0 ? connected : b.Signal.CompareTo(a.Signal); });
                return result;
            }
            finally { if (interfaces != IntPtr.Zero) WlanFreeMemory(interfaces); WlanCloseHandle(client, IntPtr.Zero); }
        }
        public static List<string> ConnectedSsids()
        {
            List<string> result;
            TryConnectedSsids(out result);
            return result;
        }
        public static bool TryConnectedSsids(out List<string> result)
        {
            result = new List<string>();
            uint version;
            IntPtr client;
            if (WlanOpenHandle(2, IntPtr.Zero, out version, out client) != 0) return false;
            IntPtr list = IntPtr.Zero;
            try
            {
                if (WlanEnumInterfaces(client, IntPtr.Zero, out list) != 0) return false;
                int count = Marshal.ReadInt32(list);
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = IntPtr.Add(list, 8 + i * 532);
                    if (Marshal.ReadInt32(item, 528) != 1) continue;
                    Guid id = (Guid)Marshal.PtrToStructure(item, typeof(Guid));
                    uint size; int type; IntPtr data;
                    if (WlanQueryInterface(client, ref id, 7, IntPtr.Zero, out size, out data, out type) != 0) return false;
                    try
                    {
                        if (size < 556) return false;
                        int length = Marshal.ReadInt32(data, 520);
                        if (length < 1 || length > 32) return false;
                        var bytes = new byte[length];
                        Marshal.Copy(IntPtr.Add(data, 524), bytes, 0, length);
                        result.Add(Encoding.UTF8.GetString(bytes));
                    }
                    finally { WlanFreeMemory(data); }
                }
            }
            finally
            {
                if (list != IntPtr.Zero) WlanFreeMemory(list);
                WlanCloseHandle(client, IntPtr.Zero);
            }
            return true;
        }
        // 「忘记网络」会删掉 Windows 里保存的配置（连同 Wi-Fi 密码）。netsh 无法凭空
        // 建立连接，重试也不会成功，必须由用户在系统里重新保存这个网络。
        public sealed class WifiProfileMissingException : InvalidOperationException
        {
            public WifiProfileMissingException(string message) : base(message) { }
        }
        // netsh wlan show profile 的退出码能干净地区分「配置存在(0)」与「不存在(1)」，
        // 而 connect 的退出码把「配置缺失」和「信号不在范围内」混为一谈。
        public static bool ProfileExists(string profile)
        {
            if (String.IsNullOrWhiteSpace(profile)) return false;
            var start = new ProcessStartInfo("netsh.exe", "wlan show profile name=" + Validation.QuoteArgument(profile));
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            try
            {
                using (var process = Process.Start(start))
                {
                    for (int i = 0; !process.HasExited; i++)
                    {
                        // 查不出来就当作存在：宁可让真正的连接流程去失败，也不要误报配置缺失。
                        if (i >= 20) { try { process.Kill(); } catch (InvalidOperationException) { } return true; }
                        System.Threading.Thread.Sleep(100);
                    }
                    return process.ExitCode == 0;
                }
            }
            catch (Exception) { return true; }
        }
        public static async Task ConnectAsync(string ssid, string profile, CancellationToken token, Func<bool> mayConnect = null)
        {
            if (String.IsNullOrWhiteSpace(ssid)) return;
            if (ConnectedSsids().Contains(ssid)) return;
            string name = String.IsNullOrWhiteSpace(profile) ? ssid : profile;
            if (!ProfileExists(name))
                throw new WifiProfileMissingException("Windows 里没有保存名为“" + name + "”的 Wi-Fi。忘记网络、或从未连接过这个 Wi-Fi 时会出现这种情况：请先在系统设置里手动连接一次（需要输入 Wi-Fi 密码），之后本工具才能自动连接。");
            if (mayConnect != null && !mayConnect()) throw new NetworkChangedException();
            var start = new ProcessStartInfo("netsh.exe", "wlan connect name=" + Validation.QuoteArgument(name) + " ssid=" + Validation.QuoteArgument(ssid));
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            using (var process = Process.Start(start))
            {
                for (int i = 0; !process.HasExited; i++)
                {
                    if (mayConnect != null && !mayConnect())
                    {
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        throw new NetworkChangedException();
                    }
                    if (token.IsCancellationRequested || i >= 40)
                    {
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        token.ThrowIfCancellationRequested();
                        throw new TimeoutException("Windows 连接 Wi-Fi 超时，请检查无线网卡和 WLAN 服务。");
                    }
                    // Recheck cancellation in the next iteration so the child is stopped first.
                    await Task.Delay(250);
                }
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Windows 未能连接此 Wi-Fi。请先在系统中手动连接一次并保存网络；若配置名称与 SSID 不同，请在高级设置中填写配置名称。");
            }
            for (int i = 0; i < 20; i++)
            {
                token.ThrowIfCancellationRequested();
                if (mayConnect != null && !mayConnect()) throw new NetworkChangedException();
                if (ConnectedSsids().Contains(ssid)) return;
                await Task.Delay(1000, token);
            }
            throw new TimeoutException("未连接到指定 SSID。请检查 Wi-Fi 是否在范围内、系统是否已保存它，以及 Wi-Fi 配置名称是否正确。");
        }
    }

    public sealed class NetworkChangedException : Exception { }

    public static class Internet
    {
        public static async Task<bool> CheckAsync(CancellationToken token)
        {
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4), MaxResponseContentBufferSize = 4096 })
            using (var probes = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                var first = ProbeAsync(client, "http://www.msftconnecttest.com/connecttest.txt", false, probes.Token);
                var second = ProbeAsync(client, "https://www.gstatic.com/generate_204", true, probes.Token);
                var remaining = new List<Task<bool>> { first, second };
                while (remaining.Count > 0)
                {
                    var done = await Task.WhenAny(remaining);
                    remaining.Remove(done);
                    token.ThrowIfCancellationRequested();
                    if (await done)
                    {
                        probes.Cancel();
                        await Task.WhenAll(first, second);
                        return true;
                    }
                }
                token.ThrowIfCancellationRequested();
                return false;
            }
        }
        static async Task<bool> ProbeAsync(HttpClient client, string url, bool noContent, CancellationToken token)
        {
            try
            {
                // Buffer a bounded response so the HttpClient timeout also covers the body.
                using (var response = await client.GetAsync(url, token))
                {
                    if (noContent) return response.StatusCode == System.Net.HttpStatusCode.NoContent;
                    if (response.StatusCode != System.Net.HttpStatusCode.OK) return false;
                    return (await response.Content.ReadAsStringAsync()).Trim() == "Microsoft Connect Test";
                }
            }
            catch (HttpRequestException) { return false; }
            catch (TaskCanceledException) { return false; }
            catch (IOException) { return false; }
        }
    }

    [DataContract]
    public sealed class PortalInput
    {
        [DataMember(Name="origin")] public string Origin;
        [DataMember(Name="username")] public string Username;
        [DataMember(Name="password")] public string Password;
        [DataMember(Name="usernameSelector")] public string UsernameSelector;
        [DataMember(Name="passwordSelector")] public string PasswordSelector;
        [DataMember(Name="loginSelector")] public string LoginSelector;
        [DataMember(Name="provider")] public string Provider;
        [DataMember(Name="submit")] public bool Submit;
    }
    [DataContract]
    public sealed class PortalReport
    {
        [DataMember(Name="state")] public string State;
        [DataMember(Name="message")] public string Message;
    }
    public static class PortalScript
    {
        public static string Build(Settings settings, bool submit)
        {
            string script;
            using (var stream = typeof(PortalScript).Assembly.GetManifestResourceStream("CampusConnect.portal.js"))
            using (var reader = new StreamReader(stream)) script = reader.ReadToEnd();
            var data = new PortalInput { Origin = Validation.Portal(settings.PortalUrl).GetLeftPart(UriPartial.Authority), Username = settings.Username, Password = settings.Password,
                UsernameSelector = settings.UsernameSelector, PasswordSelector = settings.PasswordSelector,
                LoginSelector = settings.LoginSelector, Provider = settings.Provider ?? "", Submit = submit };
            return "(" + script + ")(" + Json.Write(data) + ")";
        }
    }
}
