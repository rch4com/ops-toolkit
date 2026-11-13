namespace OpsToolkit.Shared.Models;

public class PresetProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public PresetProfile()
    {
    }

    public PresetProfile(string name, string description, string settingsJson, bool isDefault = false)
    {
        Name = name;
        Description = description;
        SettingsJson = settingsJson;
        IsDefault = isDefault;
        CreatedAt = DateTimeOffset.Now;
        UpdatedAt = DateTimeOffset.Now;
    }
}
