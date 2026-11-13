namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact cleanup. Lives on the MRContactRule entity.
/// Dead xenos are deleted after BodyTime, xeno acid blood decals after DecalTime.
/// </summary>
[RegisterComponent, Access(typeof(ContactCleanupSystem))]
public sealed partial class ContactCleanupComponent : Component
{
    [DataField]
    public TimeSpan BodyTime = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan DecalTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Decal prototypes starting with this are xeno blood (RMCDecalBloodXenonidSplatter1-6).
    /// </summary>
    [DataField]
    public string DecalPrefix = "RMCDecalBloodXenonid";
}