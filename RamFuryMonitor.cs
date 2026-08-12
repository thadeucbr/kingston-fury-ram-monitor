using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal sealed class RamFuryMonitor : IDisposable
{
    private const string ConfigPath = @"C:\ProgramData\RamFuryMonitor\config.json";
    private const string WsUrl = "ws://127.0.0.1:55599/";
    private const string Origin = "ksws-dramledctrl://5E7EFB96-6632-40D5-882F-51CE1E62CA3F";
    private const string Passphrase = "3m23s45i599";
    private const int SlotCount = 4;
    private const int LedsPerSlot = 12;
    private const int TotalLeds = SlotCount * LedsPerSlot;
    private const int Levels = LedsPerSlot;
    private const int MinSendIntervalMs = 200;

    private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
    private readonly bool _live;
    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
    private ClientWebSocket _ws;
    private Dictionary<string, object> _originalRoot;
    private Dictionary<string, object> _currentRoot;
    private int _lastLevel = -1;
    private int _lastColorBucket = -1;
    private string _lastPalette;
    private string _lastCustomColor;
    private int _lastBrightness = -2;
    private DateTime _lastSend = DateTime.MinValue;
    private bool _effectApplied;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile,
            ullAvailPageFile, ullTotalVirtual, ullAvailVirtual,
            ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);

    internal RamFuryMonitor(bool live) { _live = live; }

    public static int Main(string[] args)
    {
        if (args.Any(x => string.Equals(x, "--service", StringComparison.OrdinalIgnoreCase)))
        {
            ServiceBase.Run(new RamFuryWindowsService());
            return 0;
        }
        bool live = args.Any(x => string.Equals(x, "--live", StringComparison.OrdinalIgnoreCase));
        Console.WriteLine("RAM FURY monitor | mode=" + (live ? "LIVE" : "DRY-RUN"));
        try
        {
            using (var app = new RamFuryMonitor(live))
            {
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; app._stop.Cancel(); };
                app.RunAsync().GetAwaiter().GetResult();
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    internal async Task RunAsync()
    {
        await ConnectAndLoadAsync();
        Console.WriteLine("Connected. Original LED state captured.");
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                MonitorSettings settings = MonitorSettings.Load(ConfigPath);
                if (!settings.Enabled)
                {
                    if (_live && _effectApplied && _ws != null && _ws.State == WebSocketState.Open)
                    {
                        await SendRootAsync(Clone(_originalRoot), _stop.Token);
                        _effectApplied = false;
                        Console.WriteLine("Effect disabled; original state restored.");
                    }
                    await Task.Delay(500, _stop.Token);
                    continue;
                }
                int usage = ReadMemoryLoad();
                int level = (int)Math.Round(usage * Levels / 100.0, MidpointRounding.AwayFromZero);
                int colorBucket = usage / 5;
                Console.WriteLine("RAM={0}% Nível={1}/{2} LEDs por stick={1} Paleta={3} Brilho={4}", usage, level, Levels, settings.Palette, settings.Brightness < 0 ? "original" : settings.Brightness.ToString());
                bool settingsChanged = !string.Equals(settings.Palette, _lastPalette, StringComparison.OrdinalIgnoreCase) || settings.Brightness != _lastBrightness || !string.Equals(settings.Color, _lastCustomColor, StringComparison.OrdinalIgnoreCase);
                if ((level != _lastLevel || colorBucket != _lastColorBucket || settingsChanged) && (DateTime.UtcNow - _lastSend).TotalMilliseconds >= MinSendIntervalMs)
                {
                    await ApplyFrameAsync(level, usage, settings.Palette, settings.Brightness, settings.Color);
                    _lastLevel = level;
                    _lastColorBucket = colorBucket;
                    _lastPalette = settings.Palette;
                    _lastCustomColor = settings.Color;
                    _lastBrightness = settings.Brightness;
                    _lastSend = DateTime.UtcNow;
                }
                await Task.Delay(500, _stop.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.Error.WriteLine("LOOP_ERROR: " + ex.Message); }
        if (_live && _ws != null && _ws.State == WebSocketState.Open)
        {
            Console.WriteLine("Restoring original LED state...");
            await SendRootAsync(Clone(_originalRoot), CancellationToken.None);
            Console.WriteLine("Original LED state restored.");
        }
        if (_ws != null) _ws.Dispose();
    }

    private async Task ConnectAndLoadAsync()
    {
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("Origin", Origin);
        await _ws.ConnectAsync(new Uri(WsUrl), _stop.Token);
        Dictionary<string, object> version = await RequestAsync("get_version");
        Console.WriteLine("FURY CTRL version=" + version["version"]);
        _currentRoot = await RequestRootAsync("get_dram_led");
        _originalRoot = Clone(_currentRoot);
        ValidateState(_currentRoot);
    }

    private async Task ReconnectSessionAsync()
    {
        if (_ws != null) _ws.Dispose();
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("Origin", Origin);
        await _ws.ConnectAsync(new Uri(WsUrl), _stop.Token);
        await RequestAsync("get_version");
    }

    private async Task<Dictionary<string, object>> RequestAsync(string api)
    {
        Dictionary<string, object> root = await RequestRootAsync(api);
        return root;
    }

    private async Task<Dictionary<string, object>> RequestRootAsync(string api)
    {
        var requestRoot = new Dictionary<string, object>();
        requestRoot["api"] = api;
        var envelope = new Dictionary<string, object>();
        envelope["root"] = requestRoot;
        await SendEncryptedAsync(envelope, _stop.Token);
        Dictionary<string, object> response = await ReceiveRootAsync(_stop.Token);
        object status;
        if (response.TryGetValue("status", out status) && Convert.ToString(status) != "0")
            throw new InvalidOperationException("FURY service returned status " + status);
        return response;
    }

    private async Task SendRootAsync(Dictionary<string, object> root, CancellationToken token)
    {
        var envelope = new Dictionary<string, object>();
        envelope["root"] = root;
        await SendEncryptedAsync(envelope, token);
        Dictionary<string, object> response = await ReceiveRootAsync(token);
        if (response.ContainsKey("status") && Convert.ToString(response["status"]) != "0")
            throw new InvalidOperationException("set_dram_led status=" + response["status"]);
    }

    private async Task ApplyFrameAsync(int level, int usage, string palette, int brightness, string customColor)
    {
        Dictionary<string, object> next = Clone(_originalRoot);
        var slots = (Dictionary<string, object>)next["ctrl_settings_ddr5"];
        object[] color = UsageColor(usage, palette, customColor);
        for (int slot = 0; slot < SlotCount; slot++)
        {
            var state = (Dictionary<string, object>)slots["slot_" + slot];
            if (brightness >= 0) state["brightness"] = Math.Max(0, Math.Min(100, brightness));
            var colors = new object[LedsPerSlot];
            for (int led = 0; led < LedsPerSlot; led++)
            {
                // FURY LED index 0 is the physical bottom LED on this setup.
                bool on = led < level;
                colors[led] = on ? color : new object[] { 0, 0, 0 };
            }
            state["ctrl_color"] = colors;
        }
        next["api"] = "set_dram_led";
        if (_live)
        {
            await SendRootAsync(next, _stop.Token);
            _effectApplied = true;
            await ReconnectSessionAsync();
        }
        _currentRoot = next;
    }

    private async Task SendEncryptedAsync(Dictionary<string, object> message, CancellationToken token)
    {
        string plain = _json.Serialize(message);
        byte[] cipher = Encrypt(Encoding.UTF8.GetBytes(plain), Encoding.UTF8.GetBytes(Passphrase));
        await _ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(Convert.ToBase64String(cipher))), WebSocketMessageType.Text, true, token);
    }

    private async Task<Dictionary<string, object>> ReceiveRootAsync(CancellationToken token)
    {
        using (var ms = new MemoryStream())
        {
            var buffer = new byte[4096];
            WebSocketReceiveResult result;
            do
            {
                result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("FURY WebSocket closed");
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            byte[] cipher = Convert.FromBase64String(Encoding.UTF8.GetString(ms.ToArray()));
            string plain = Encoding.UTF8.GetString(Decrypt(cipher, Encoding.UTF8.GetBytes(Passphrase)));
            var envelope = (Dictionary<string, object>)_json.DeserializeObject(plain);
            return (Dictionary<string, object>)envelope["root"];
        }
    }

    private static byte[] Encrypt(byte[] plain, byte[] pass)
    {
        byte[] salt = RandomBytes(32), iv = RandomBytes(32), key;
        using (var d = new Rfc2898DeriveBytes(pass, salt, 1000)) key = d.GetBytes(32);
        byte[] cipher;
        using (var rij = NewRijndael())
        using (var ms = new MemoryStream())
        using (var cs = new CryptoStream(ms, rij.CreateEncryptor(key, iv), CryptoStreamMode.Write))
        { cs.Write(plain, 0, plain.Length); cs.FlushFinalBlock(); cipher = ms.ToArray(); }
        return salt.Concat(iv).Concat(cipher).ToArray();
    }

    private static byte[] Decrypt(byte[] all, byte[] pass)
    {
        byte[] salt = all.Take(32).ToArray(), iv = all.Skip(32).Take(32).ToArray();
        byte[] cipher = all.Skip(64).ToArray(), key;
        using (var d = new Rfc2898DeriveBytes(pass, salt, 1000)) key = d.GetBytes(32);
        using (var rij = NewRijndael())
        using (var input = new MemoryStream(cipher))
        using (var cs = new CryptoStream(input, rij.CreateDecryptor(key, iv), CryptoStreamMode.Read))
        using (var output = new MemoryStream())
        { cs.CopyTo(output); return output.ToArray(); }
    }

    private static RijndaelManaged NewRijndael()
    {
        return new RijndaelManaged { BlockSize = 256, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 };
    }

    private static byte[] RandomBytes(int n)
    {
        byte[] b = new byte[n];
        using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(b);
        return b;
    }

    private Dictionary<string, object> Clone(Dictionary<string, object> value)
    { return (Dictionary<string, object>)_json.DeserializeObject(_json.Serialize(value)); }

    private static object[] UsageColor(int usage, string palette, string customColor)
    {
        usage = Math.Max(0, Math.Min(100, usage));
        palette = (palette ?? "traffic").ToLowerInvariant();
        if (palette == "custom")
        {
            object[] parsed;
            if (TryParseHex(customColor, out parsed)) return parsed;
        }
        if (palette == "green") return new object[] { 0, 255, 0 };
        if (palette == "blue") return new object[] { 0, 128, 255 };
        if (palette == "purple") return new object[] { 190, 0, 255 };
        if (palette == "oldgold") return new object[] { 140, 80, 0 };
        if (usage <= 50)
        {
            int red = (int)Math.Round(255.0 * usage / 50.0);
            return new object[] { red, 255, 0 };
        }
        int green = (int)Math.Round(255.0 * (100 - usage) / 50.0);
        return new object[] { 255, green, 0 };
    }

    private static bool TryParseHex(string value, out object[] color)
    {
        color = null;
        if (String.IsNullOrWhiteSpace(value)) return false;
        string hex = value.Trim().TrimStart('#');
        int r, g, b;
        if (hex.Length != 6 || !Int32.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r) || !Int32.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g) || !Int32.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) return false;
        color = new object[] { r, g, b };
        return true;
    }

    private static int ReadMemoryLoad()
    {
        var s = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
        if (!GlobalMemoryStatusEx(ref s)) throw new InvalidOperationException("GlobalMemoryStatusEx failed");
        return (int)s.dwMemoryLoad;
    }

    private static void ValidateState(Dictionary<string, object> root)
    {
        var slots = root["ctrl_settings_ddr5"] as Dictionary<string, object>;
        if (slots == null || slots.Count != SlotCount) throw new InvalidOperationException("Unexpected DDR5 slot state");
        foreach (var pair in slots)
        {
            var state = (Dictionary<string, object>)pair.Value;
            var colors = state["ctrl_color"] as object[];
            if (colors == null || colors.Length != LedsPerSlot) throw new InvalidOperationException("Unexpected LED state in " + pair.Key);
        }
    }

    internal void Stop() { _stop.Cancel(); }
    public void Dispose() { _stop.Cancel(); if (_ws != null) _ws.Dispose(); _stop.Dispose(); }
}

internal sealed class MonitorSettings
{
    public bool Enabled = true;
    public string Palette = "traffic";
    public int Brightness = -1;
    public string Color;

    public static MonitorSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new MonitorSettings();
            var serializer = new JavaScriptSerializer();
            var data = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            var result = new MonitorSettings();
            object enabled;
            object palette;
            object brightness;
            object color;
            if (data.TryGetValue("enabled", out enabled)) result.Enabled = Convert.ToBoolean(enabled);
            if (data.TryGetValue("palette", out palette)) result.Palette = Convert.ToString(palette);
            if (data.TryGetValue("brightness", out brightness)) result.Brightness = Convert.ToInt32(brightness);
            if (data.TryGetValue("color", out color)) result.Color = Convert.ToString(color);
            return result;
        }
        catch { return new MonitorSettings(); }
    }
}

internal sealed class RamFuryWindowsService : ServiceBase
{
    private RamFuryMonitor _monitor;
    private Task _worker;
    private StreamWriter _log;
    private CancellationTokenSource _serviceStop;

    public RamFuryWindowsService()
    {
        ServiceName = "RamFuryMonitor";
        CanStop = true;
        CanShutdown = true;
        AutoLog = false;
    }

    protected override void OnStart(string[] args)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RamFuryMonitor");
        Directory.CreateDirectory(dir);
        _log = new StreamWriter(Path.Combine(dir, "monitor.log"), true, Encoding.UTF8) { AutoFlush = true };
        Console.SetOut(_log);
        Console.SetError(_log);
        _serviceStop = new CancellationTokenSource();
        _worker = Task.Run(async () =>
        {
            while (!_serviceStop.IsCancellationRequested)
            {
                _monitor = new RamFuryMonitor(true);
                try { await _monitor.RunAsync(); }
                catch (Exception ex) { Console.Error.WriteLine("SERVICE_ERROR: " + ex); }
                finally { _monitor.Dispose(); _monitor = null; }
                if (!_serviceStop.IsCancellationRequested)
                {
                    try { await Task.Delay(TimeSpan.FromSeconds(10), _serviceStop.Token); }
                    catch (OperationCanceledException) { }
                }
            }
        });
    }

    protected override void OnStop()
    {
        if (_serviceStop != null) _serviceStop.Cancel();
        if (_monitor != null) _monitor.Stop();
        if (_worker != null) _worker.Wait(TimeSpan.FromSeconds(15));
        if (_monitor != null) _monitor.Dispose();
        if (_log != null) { _log.Flush(); _log.Dispose(); }
        if (_serviceStop != null) _serviceStop.Dispose();
    }

    protected override void OnShutdown() { OnStop(); base.OnShutdown(); }
}
