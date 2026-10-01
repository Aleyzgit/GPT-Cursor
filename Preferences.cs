using System.Text.Json;

namespace GPTCursor;

internal sealed class Preferences : AnimationOptions
{
    public int Size { get; set; } = 32;
    public int Fps { get; set; } = 240;
    public bool AllPointers { get; set; }
    public string Language { get; set; } = "en";
    public bool ShortcutEnabled { get; set; } = true;
    public uint ShortcutModifiers { get; set; } = 3;
    public uint ShortcutKey { get; set; } = (uint)Keys.C;
    internal static readonly int[] FrameRates = [60, 120, 144, 165, 240, 360];
    internal static string FilePath => File.Exists(Path.Combine(AppContext.BaseDirectory, "installed.flag"))
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPTCursor", "settings.json")
        : Path.Combine(AppContext.BaseDirectory, "settings.json");
    internal static Preferences Load()
    {
        Preferences value;
        try { value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new(); }
        catch { value = new(); }
        value.Size = Math.Clamp(value.Size, 24, 64);
        if (!FrameRates.Contains(value.Fps)) value.Fps = 240;
        if (value.Language != "de") value.Language = "en";
        if (!GlobalShortcut.Valid(value.ShortcutModifiers, value.ShortcutKey))
        { value.ShortcutModifiers = 3; value.ShortcutKey = (uint)Keys.C; }
        if (!Enum.IsDefined(value.PositionMethod)) value.PositionMethod = SmoothingMethod.Sine;
        if (!Enum.IsDefined(value.EffectsMethod)) value.EffectsMethod = SmoothingMethod.Responsive;
        return value;
    }
}
