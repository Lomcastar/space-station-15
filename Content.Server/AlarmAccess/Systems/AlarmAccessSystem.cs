using Content.Server.AlarmAccess.Components;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.AlarmAccess.Systems;

public sealed class AlarmAccessSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        // If there are no alarm conditions, we restore access to the original (green, blue...)
        if (!TryComp<AlarmAccessComponent>(args.Station, out var alarmAccess) ||
            !alarmAccess.AlarmLevels.ContainsKey(args.AlertLevel))
        {
            RevertDoors(args.Station);
            return;
        }

        //Copy original access
        SnapshotDoors(args.Station);

        //Change original access
        GrantAccess(args.Station, args.AlertLevel);
    }

    /// <summary>
    /// Transfers all the doors to restore to the original
    /// </summary>
    private void RevertDoors(EntityUid stationUid)
    {
        var doorQuery = EntityQuery<DoorComponent, TransformComponent>();
        foreach (var (_, xform) in doorQuery)
        {
            var doorUid = xform.Owner;

            var owningStation = _stationSystem.GetOwningStation(doorUid);
            if (owningStation is null || owningStation.Value != stationUid)
                continue;

            if (!_access.GetMainAccessReader(doorUid, out var accessEnt))
                continue;

            _access.TryRestoreOriginal(accessEnt.Value);
        }
    }

    /// <summary>
    /// Passing the doors to create a copy
    /// </summary>
    private void SnapshotDoors(EntityUid stationUid)
    {
        var doorQuery = EntityQuery<DoorComponent, TransformComponent>();
        foreach (var (_, xform) in doorQuery)
        {
            var doorUid = xform.Owner;

            var owningStation = _stationSystem.GetOwningStation(doorUid);
            if (owningStation is null || owningStation.Value != stationUid)
                continue;

            if (!_access.GetMainAccessReader(doorUid, out var accessEnt))
                continue;

            // Overwrite protection to save the original
            // example: Red(RedAcc)->Gamma(GammaAcc)->Green(RedAcc)
            if (accessEnt.Value.Comp.AccessListsOriginal is null)
                _access.TrySnapshotOriginal(accessEnt.Value);
        }
    }

    private void GrantAccess(EntityUid stationUid, string alertLevel)
    {
        if (!TryComp<AlarmAccessComponent>(stationUid, out var alarmAccess))
            return;

        if (!alarmAccess.AlarmLevels.TryGetValue(alertLevel, out var accessInfo))
            return;

        var grantAccessTags = new HashSet<ProtoId<AccessLevelPrototype>>();
        foreach (var group in accessInfo.GrantAccessGroups)
        {
            if (_prototype.TryIndex(group, out var groupProto))
                grantAccessTags.UnionWith(groupProto.Tags);
        }

        var effectiveBlacklist = new HashSet<ProtoId<AccessLevelPrototype>>(accessInfo.Blacklist);
        foreach (var group in accessInfo.BlacklistGroups)
        {
            if (_prototype.TryIndex(group, out var groupProto))
                effectiveBlacklist.UnionWith(groupProto.Tags);
        }

        var doorQuery = EntityQuery<DoorComponent, TransformComponent>();
        foreach (var (_, xform) in doorQuery)
        {
            var doorUid = xform.Owner;

            var owningStation = _stationSystem.GetOwningStation(doorUid);
            if (owningStation is null || owningStation.Value != stationUid)
                continue;

            if (!_access.GetMainAccessReader(doorUid, out var accessEnt))
                continue;

            var accessComp = accessEnt.Value.Comp;

            if (!_access.AreAccessTagsAllowed(grantAccessTags, accessComp))
                continue;

            if (_access.AreAccessTagsAllowed(effectiveBlacklist, accessComp))
                continue;

            // Добавляем уровни одним батчем
            _access.TryAddAccesses(accessEnt.Value, accessInfo.TargetAccess);
        }
    }
}
