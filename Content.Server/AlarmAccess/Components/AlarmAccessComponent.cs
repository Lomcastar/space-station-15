using Content.Server.AlarmAccess.Systems;
using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Server.AlarmAccess.Components;

[RegisterComponent, Access(typeof(AlarmAccessSystem))]
public sealed partial class AlarmAccessComponent : Component
{
    [DataField]
    public Dictionary<string, AlarmAccessInfo> AlarmLevels = new();
}

public sealed partial class AlarmAccessInfo
{
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> TargetAccess = new();

    [DataField]
    public List<ProtoId<AccessGroupPrototype>> GrantAccessGroups = new();

    [DataField]
    public List<ProtoId<AccessGroupPrototype>> BlacklistGroups = new();

    [DataField]
    public List<ProtoId<AccessLevelPrototype>> Blacklist = new();
}
