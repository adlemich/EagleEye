using System.Globalization;
using System.Text.Json;

namespace OverlaySpike;

/// <summary>Overlay corner.</summary>
internal enum Corner
{
    TopRight,
    BottomRight,
    BottomLeft,
    TopLeft,
}

/// <summary>Effective settings. Precedence: command line &gt; overlay-spike.json &gt; defaults. Never written back.</summary>
internal sealed class SpikeSettings
{
    public const string FileName = "overlay-spike.json";

    public int StartMinutes { get; set; } = 15;

    public string Label { get; set; } = "Spiele";

    public Corner Corner { get; set; } = Corner.TopRight;

    public int Opacity { get; set; } = 50;

    public bool Uniform { get; set; }

    public bool Reassert { get; set; } = true;

    public bool Beep { get; set; } = true;

    public float FontSizePt { get; set; } = 16;

    public int CpuLogSeconds { get; set; } = 30;

    /// <summary>Source of each setting: default, file or cmdline.</summary>
    public Dictionary<string, string> Sources { get; } = new(StringComparer.Ordinal)
    {
        ["startMinutes"] = "default", ["label"] = "default", ["corner"] = "default", ["opacity"] = "default",
        ["alpha"] = "default", ["reassert"] = "default", ["beep"] = "default", ["fontSizePt"] = "default",
        ["cpuLogSeconds"] = "default",
    };

    /// <summary>Loads the settings; problems are added to <paramref name="notes"/> (logged later).</summary>
    public static SpikeSettings Load(string exeDir, string[] args, List<string> notes)
    {
        var s = new SpikeSettings();
        var path = Path.Combine(exeDir, FileName);
        if (File.Exists(path))
        {
            try
            {
                s.ApplyFile(File.ReadAllText(path), notes);
                notes.Add($"config file read: {path}");
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                notes.Add($"config file ignored ({ex.GetType().Name}: {ex.Message})");
            }
        }
        else
        {
            notes.Add($"no config file ({path})");
        }

        s.ApplyArgs(args, notes);
        s.Opacity = Math.Clamp(s.Opacity, 30, 90);
        s.StartMinutes = Math.Clamp(s.StartMinutes, 1, 600);
        s.FontSizePt = Math.Clamp(s.FontSizePt, 8, 72);
        s.CpuLogSeconds = Math.Clamp(s.CpuLogSeconds, 5, 3600);
        return s;
    }

    public override string ToString() =>
        $"startMinutes={StartMinutes} ({Sources["startMinutes"]}), label=\"{Label}\" ({Sources["label"]}), corner={Corner} ({Sources["corner"]}), " +
        $"opacity={Opacity}% ({Sources["opacity"]}), alpha={(Uniform ? "uniform" : "perPixel")} ({Sources["alpha"]}), " +
        $"reassert={Reassert} ({Sources["reassert"]}), beep={Beep} ({Sources["beep"]}), fontSizePt={FontSizePt.ToString(CultureInfo.InvariantCulture)} ({Sources["fontSizePt"]}), " +
        $"cpuLogSeconds={CpuLogSeconds} ({Sources["cpuLogSeconds"]})";

    private void ApplyFile(string json, List<string> notes)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            var v = p.Value;
            var ok = p.Name.ToLowerInvariant() switch
            {
                "startminutes" => Set("startMinutes", "file", () => StartMinutes = v.GetInt32()),
                "label" => Set("label", "file", () => Label = v.GetString() ?? Label),
                "corner" => Set("corner", "file", () => Corner = Enum.Parse<Corner>(v.GetString() ?? string.Empty, true)),
                "opacity" => Set("opacity", "file", () => Opacity = v.GetInt32()),
                "alpha" => Set("alpha", "file", () => Uniform = ParseAlpha(v.GetString())),
                "reassert" => Set("reassert", "file", () => Reassert = v.GetBoolean()),
                "beep" => Set("beep", "file", () => Beep = v.GetBoolean()),
                "fontsizept" => Set("fontSizePt", "file", () => FontSizePt = v.GetSingle()),
                "cpulogseconds" => Set("cpuLogSeconds", "file", () => CpuLogSeconds = v.GetInt32()),
                _ => false,
            };
            if (!ok)
            {
                notes.Add($"config key ignored: {p.Name}");
            }
        }
    }

    private void ApplyArgs(string[] args, List<string> notes)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            var next = i + 1 < args.Length ? args[i + 1] : string.Empty;
            var ok = a switch
            {
                "--minutes" => Set("startMinutes", "cmdline", () => StartMinutes = int.Parse(next, CultureInfo.InvariantCulture)) && Skip(ref i),
                "--label" => Set("label", "cmdline", () => Label = next) && Skip(ref i),
                "--corner" => Set("corner", "cmdline", () => Corner = Enum.Parse<Corner>(next, true)) && Skip(ref i),
                "--opacity" => Set("opacity", "cmdline", () => Opacity = int.Parse(next, CultureInfo.InvariantCulture)) && Skip(ref i),
                "--alpha" => Set("alpha", "cmdline", () => Uniform = ParseAlpha(next)) && Skip(ref i),
                "--no-reassert" => Set("reassert", "cmdline", () => Reassert = false),
                "--no-beep" => Set("beep", "cmdline", () => Beep = false),
                _ => false,
            };
            if (!ok)
            {
                notes.Add($"command line argument ignored: {args[i]}");
            }
        }
    }

    private static bool Skip(ref int i)
    {
        i++;
        return true;
    }

    private static bool ParseAlpha(string? value) => value?.ToLowerInvariant() switch
    {
        "uniform" => true,
        "perpixel" => false,
        _ => throw new FormatException($"alpha must be perPixel or uniform, not '{value}'"),
    };

    private bool Set(string key, string source, Action apply)
    {
        try
        {
            apply();
            Sources[key] = source;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return false;
        }
    }
}
