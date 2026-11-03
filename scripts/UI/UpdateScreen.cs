using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

namespace TheFartherWeStray.UI;

/// <summary>
/// The first thing a downloaded Windows copy does (the user's ask, 2026-11-03, the way The One He
/// Missed does it, ported from its UpdateScreen): checks ecec.dev for a newer version and, if there
/// is one, installs it and restarts, no questions. How a release is laid out is in docs/releasing.md.
/// In short: ecec.dev/the-farther-we-stray/version.json redirects to the newest GitHub Release's
/// version.json, which names a small patch (the .pck and the game's own DLL: everything a normal
/// change touches) and the full download (needed only when the Godot engine itself changed).
///
/// The running game can't overwrite itself on Windows (its DLL and .pck are open), so the new files
/// are unpacked to user://update/ and a small script waits for the game to close, copies them over,
/// and starts it again.
///
/// It isn't a screen of its own: the title lays it over itself before anything else and carries on
/// when it's done; nothing on the title takes input meanwhile. Every failure, from no internet
/// onwards, just goes to the title. An update is never worth not being able to play. Android can't
/// replace itself, so it has <see cref="UpdateBanner"/> instead.
/// </summary>
public partial class UpdateScreen : Control
{
    /// <summary>The one address baked into every copy. It's on ecec.dev rather than GitHub so where releases live can change without stranding old copies.</summary>
    public const string VersionUrl = "https://ecec.dev/the-farther-we-stray/version.json";

    /// <summary>Points a build at a test server instead (docs/releasing.md).</summary>
    public const string UrlOverrideVariable = "THEFARTHERWESTRAY_UPDATE_URL";

    public static string UserAgent => $"TheFartherWeStray/{CurrentVersion}";

    public static string CurrentVersion => ProjectSettings.GetSetting("application/config/version", "0.0.0").AsString();

    public Action Finished;

    static string EngineVersion
    {
        get
        {
            var info = Engine.GetVersionInfo();
            return $"{info["major"]}.{info["minor"]}.{info["patch"]}";
        }
    }

    /// <summary>
    /// Plays the whole check against a made-up release instead of the network, in any build:
    /// <c>--update-screen=9.9.9</c>. Nothing is downloaded; it carries on to the title. For
    /// screenshots. Android's strip has <c>--update-banner</c>.
    /// </summary>
    const string TestArgPrefix = "--update-screen=";

    static string TestArg()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith(TestArgPrefix)) return arg.Substring(TestArgPrefix.Length);
        return null;
    }

    /// <summary>A test argument's version.json.</summary>
    public static string TestReleaseJson(string version) => JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object>
    {
        ["version"] = version,
        ["engine"] = "test",
        ["builds"] = new System.Collections.Generic.Dictionary<string, object> { ["windows"] = new System.Collections.Generic.Dictionary<string, string> { ["patch"] = "", ["full"] = "" } },
        ["android"] = "https://ecec.dev/the-farther-we-stray/download",
    });

    // Once per launch: going back to the title from the game doesn't check again.
    static bool _checked;

    /// <summary>Only an exported Windows build updates itself, once per launch. The editor and `godot --path .` never do; `--no-update` turns it off.</summary>
    public static bool ShouldCheck()
    {
        if (_checked) return false;
        if (TestArg() != null) return true;
        if (!OS.HasFeature("template") || OS.GetName() != "Windows") return false;
        return Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-update") < 0 && Array.IndexOf(OS.GetCmdlineArgs(), "--no-update") < 0;
    }

    enum Phase { Checking, Downloading, Installing, Restart, Done }

    // Written by the worker thread, read by the main thread: plain values, nothing to lock.
    volatile Phase _phase = Phase.Checking;
    volatile float _progress;
    string _newVersion = "";
    string _applyScript = "";

    Label _title, _detail;
    ColorRect _barBack, _barFill;
    int _frames;
    bool _finished;

    const float BarWidth = 440f, BarHeight = 18f;

    public override void _Ready()
    {
        _checked = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; // nothing on the title underneath can be clicked mid-update
        var ground = new ColorRect { Color = Color.FromHtml("#0b0914"), MouseFilter = MouseFilterEnum.Ignore };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);
        var vp = GetViewportRect().Size;
        _title = Centred(Ui.Text("", 40, UiSkin.Heading(), 10), vp.Y / 2 - 90, vp.X);
        _detail = Centred(Ui.Text("", 22, Ui.Dim, 5), vp.Y / 2 + 34, vp.X);
        _barBack = new ColorRect { Color = Color.FromHtml("#1c1826"), Position = new Vector2((vp.X - BarWidth) / 2, vp.Y / 2), Size = new Vector2(BarWidth, BarHeight), Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _barFill = new ColorRect { Color = Ui.Cyan, Position = _barBack.Position, Size = new Vector2(0, BarHeight), Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_barBack);
        AddChild(_barFill);
        Task.Run(Work);
    }

    Label Centred(Label l, float y, float width)
    {
        l.HorizontalAlignment = HorizontalAlignment.Center;
        l.Position = new Vector2(0, y);
        l.Size = new Vector2(width, 60);
        AddChild(l);
        return l;
    }

    // Keys and buttons don't reach the title's menu while this is up.
    public override void _Input(InputEvent e)
    {
        if (!_finished && e is InputEventKey or InputEventJoypadButton) GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        _frames++;
        switch (_phase)
        {
            case Phase.Done:
                if (_finished) return;
                _finished = true;
                Finished?.Invoke();
                QueueFree();
                return;
            case Phase.Restart:
                if (_finished) return;
                _finished = true;
                // The script waits for this process to end, so it has to be started first.
                OS.CreateProcess("cmd.exe", new[] { "/c", _applyScript });
                GetTree().Quit();
                return;
            case Phase.Checking:
                // Most checks finish before anyone could read this; it shows only on a slow line, to say why the game hasn't started.
                _title.Text = _frames > 45 ? "Checking for updates..." : "";
                break;
            case Phase.Downloading:
                _title.Text = "Updating The Farther We Stray";
                _detail.Text = $"Getting version {_newVersion}...  {Mathf.RoundToInt(_progress * 100)}%";
                Bar(_progress);
                break;
            case Phase.Installing:
                _title.Text = "Updating The Farther We Stray";
                _detail.Text = "Nearly done: the game will restart by itself.";
                Bar(1);
                break;
        }
    }

    void Bar(float filled)
    {
        _barBack.Visible = _barFill.Visible = true;
        _barFill.Size = new Vector2(BarWidth * Mathf.Clamp(filled, 0, 1), BarHeight);
    }

    // ------------------------------------------------------------------ the worker

    async Task Work()
    {
        try
        {
            _phase = await Update() ? Phase.Restart : Phase.Done;
        }
        catch (Exception e)
        {
            GD.Print($"UpdateScreen: no update ({e.GetType().Name}: {e.Message})");
            _phase = Phase.Done;
        }
    }

    /// <summary>True when a new version is unpacked and the restart script is written.</summary>
    async Task<bool> Update()
    {
        string test = TestArg();
        string url = OS.GetEnvironment(UrlOverrideVariable);
        if (string.IsNullOrEmpty(url)) url = VersionUrl;
        using var doc = JsonDocument.Parse(test != null ? TestReleaseJson(test) : await FetchVersionJson(url));
        var release = doc.RootElement;
        string latest = release.GetProperty("version").GetString() ?? "";
        if (!IsNewer(latest, CurrentVersion))
        {
            GD.Print($"UpdateScreen: {CurrentVersion} is up to date");
            return false;
        }
        string installDir = Path.GetDirectoryName(OS.GetExecutablePath());
        if (test == null && !CanWriteTo(installDir))
        {
            GD.Print($"UpdateScreen: {latest} is out, but {installDir} isn't writable");
            return false;
        }
        // The patch only works on the engine it was built with; anything else takes the full download.
        bool sameEngine = release.TryGetProperty("engine", out var engine) && engine.GetString() == EngineVersion;
        if (!release.TryGetProperty("builds", out var builds) || !builds.TryGetProperty("windows", out var files))
        {
            GD.Print($"UpdateScreen: {latest} has no Windows build");
            return false;
        }
        url = sameEngine && files.TryGetProperty("patch", out var patch) ? patch.GetString() : files.GetProperty("full").GetString();
        _newVersion = latest;
        if (test != null)
        {
            GD.Print($"UpdateScreen: test release {latest}, nothing to download (--update-screen)");
            (_phase, _progress) = (Phase.Downloading, 0.6f); // as it looks part way, for screenshots
            await Task.Delay(2500);
            return false;
        }
        _phase = Phase.Downloading;

        string work = ProjectSettings.GlobalizePath("user://update");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        string zip = Path.Combine(work, "update.zip");
        await Download(url, zip);

        _phase = Phase.Installing;
        string unpacked = Path.Combine(work, "files");
        Directory.CreateDirectory(unpacked);
        ZipFile.ExtractToDirectory(zip, unpacked);
        File.Delete(zip);
        // The full download keeps everything in a folder, so it unpacks tidily for a person; the patch
        // doesn't. Either way, copy from wherever the .pck is.
        string pck = FindPck(unpacked) ?? throw new InvalidDataException("the update has no .pck");
        string from = Path.GetDirectoryName(pck);
        _applyScript = Path.Combine(work, "apply.cmd");
        File.WriteAllText(_applyScript, ApplyScript(from, installDir, OS.GetExecutablePath(), OS.GetProcessId()));
        GD.Print($"UpdateScreen: {CurrentVersion} -> {latest} ready, restarting");
        return true;
    }

    static async Task<string> FetchVersionJson(string url)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return await http.GetStringAsync(url);
    }

    async Task Download(string url, string path)
    {
        // Its own client: the five seconds above are for the check, not for a full zip on a slow line.
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? 0;
        _progress = 0;
        using var source = await response.Content.ReadAsStreamAsync();
        using var target = File.Create(path);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read));
            done += read;
            if (total > 0) _progress = done / (float)total;
        }
    }

    /// <summary>The one .pck in the unpacked download, at its top or one folder down; null if none.</summary>
    static string FindPck(string dir)
    {
        var folders = new System.Collections.Generic.List<string> { dir };
        folders.AddRange(Directory.GetDirectories(dir));
        foreach (string folder in folders)
        {
            string[] pcks = Directory.GetFiles(folder, "*.pck");
            if (pcks.Length > 1) throw new InvalidDataException($"the update has {pcks.Length} .pck files in one folder");
            if (pcks.Length == 1) return pcks[0];
        }
        return null;
    }

    static bool CanWriteTo(string dir)
    {
        try
        {
            string probe = Path.Combine(dir, ".update-probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>"0.10.0" is newer than "0.9.2": compared number by number, not as text.</summary>
    public static bool IsNewer(string candidate, string current)
    {
        string[] a = candidate.Split('.'), b = current.Split('.');
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int x = i < a.Length && int.TryParse(a[i], out int ax) ? ax : 0;
            int y = i < b.Length && int.TryParse(b[i], out int by) ? by : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    /// <summary>
    /// Waits for the game to close, copies the new files over the old, and starts it again. `ping` is
    /// the sleep because `timeout` refuses to run without a console; robocopy retries a locked file a
    /// few times, which covers the moment Windows takes to let go of a closed program's files.
    /// </summary>
    static string ApplyScript(string from, string to, string exe, int pid) =>
        "@echo off\r\n" +
        ":wait\r\n" +
        $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\r\n" +
        "if not errorlevel 1 (\r\n" +
        "  ping -n 2 127.0.0.1 >nul\r\n" +
        "  goto wait\r\n" +
        ")\r\n" +
        $"robocopy \"{from}\" \"{to}\" /E /R:10 /W:1 /NFL /NDL /NJH /NJS /NP >nul\r\n" +
        $"start \"\" \"{exe}\"\r\n" +
        $"rmdir /s /q \"{from}\"\r\n";
}
