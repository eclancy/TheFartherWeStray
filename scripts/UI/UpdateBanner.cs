using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

namespace TheFartherWeStray.UI;

/// <summary>
/// The Android half of the update check (ported from The One He Missed's): a strip across the foot of
/// the title that says a newer version is out and offers to open its APK in the browser. Nothing
/// more. An app can't install an APK without asking for REQUEST_INSTALL_PACKAGES, which is what
/// makes a sideloaded game look like malware, so the phone only tells the player: the browser
/// downloads the file, Android's installer asks, and because the new APK carries the same signature
/// and a higher version code it installs over the old one, saves and all (docs/releasing.md).
///
/// It never holds the title up: the title starts at once and the strip appears whenever the answer
/// comes, or never, which is what every failure quietly comes to. Its buttons take taps, not the
/// pad's focus, so A on the title only ever means what the menu says.
/// </summary>
public partial class UpdateBanner : CanvasLayer
{
    /// <summary>Shows the strip with this version outside an Android export, no network: <c>--update-banner=9.9.9</c> (screenshots).</summary>
    const string TestArgPrefix = "--update-banner=";

    sealed record Offer(string Version, string ApkUrl);

    // Once per launch: each visit to the title asks the same finished check, so the strip comes back
    // until it's dismissed, then stays gone until the next launch.
    static Task<Offer> _check;
    static bool _dismissed;

    Offer _offer;
    PanelContainer _panel;

    /// <summary>Adds the strip to the title if this copy should check, starting the check the first time; null when it shouldn't.</summary>
    public static UpdateBanner AttachTo(Node title)
    {
        string test = TestVersion();
        if ((test == null && !ShouldCheck()) || _dismissed) return null;
        _check ??= test != null ? Task.FromResult(new Offer(test, "https://ecec.dev/the-farther-we-stray/download")) : Task.Run(Fetch);
        var banner = new UpdateBanner { Name = "UpdateBanner" };
        title.AddChild(banner);
        return banner;
    }

    /// <summary>An exported Android build only; `--no-update` turns it off.</summary>
    static bool ShouldCheck()
    {
        if (!OS.HasFeature("template") || OS.GetName() != "Android") return false;
        return Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-update") < 0 && Array.IndexOf(OS.GetCmdlineArgs(), "--no-update") < 0;
    }

    static string TestVersion()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith(TestArgPrefix)) return arg.Substring(TestArgPrefix.Length);
        return null;
    }

    /// <summary>On a worker thread. Null for nothing to offer, whatever the reason, a release without an APK included.</summary>
    static async Task<Offer> Fetch()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UpdateScreen.UserAgent);
            string url = OS.GetEnvironment(UpdateScreen.UrlOverrideVariable);
            if (string.IsNullOrEmpty(url)) url = UpdateScreen.VersionUrl;
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
            var release = doc.RootElement;
            string latest = release.GetProperty("version").GetString() ?? "";
            if (!UpdateScreen.IsNewer(latest, UpdateScreen.CurrentVersion))
            {
                GD.Print($"UpdateBanner: {UpdateScreen.CurrentVersion} is up to date");
                return null;
            }
            if (!release.TryGetProperty("android", out var apk) || string.IsNullOrEmpty(apk.GetString()))
            {
                GD.Print($"UpdateBanner: {latest} is out, but has no Android build");
                return null;
            }
            GD.Print($"UpdateBanner: {latest} is available");
            return new Offer(latest, apk.GetString());
        }
        catch (Exception e)
        {
            GD.Print($"UpdateBanner: no update check ({e.GetType().Name}: {e.Message})");
            return null;
        }
    }

    public override void _Ready()
    {
        Layer = 6;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_panel != null) return;
        if (_check == null || !_check.IsCompleted) return;
        _offer = _check.Result;
        if (_offer == null) { SetProcess(false); return; }
        Build();
        Visible = true;
        SetProcess(false);
    }

    void Build()
    {
        // Along the foot of the screen, clear of the game's name at the top, growing up to fit.
        _panel = new PanelContainer
        {
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = 12, OffsetRight = -12, OffsetTop = -12, OffsetBottom = -12,
            GrowVertical = Control.GrowDirection.Begin, MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _panel.AddThemeStyleboxOverride("panel", UiSkin.Lit(UiPart.Panel, Ui.Cyan));
        AddChild(_panel);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(row);
        var message = Ui.Text($"Version {_offer.Version} is out", 20, Ui.Ink, 5);
        message.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(message);
        var download = Button("Download");
        download.Pressed += () => OS.ShellOpen(_offer.ApkUrl); // the strip stays: backing out of the download leaves the button
        row.AddChild(download);
        var later = Button("Later");
        later.Pressed += () => { _dismissed = true; Visible = false; };
        row.AddChild(later);
    }

    static Button Button(string text)
    {
        // Taps only, never the pad's focus (PadNav leaves a button marked so alone).
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 48), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        b.SetMeta(PadNav.Skip, true);
        b.AddThemeFontSizeOverride("font_size", 20);
        return b;
    }
}
