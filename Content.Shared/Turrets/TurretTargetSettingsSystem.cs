using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared.Turrets;

public sealed partial class TurretTargetSettingsSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void AddAccessLevelExemption(Entity<TurretTargetSettingsComponent> ent, ProtoId<AccessLevelPrototype> exemption)
    {
        ent.Comp.ExemptAccessLevels.Add(exemption);
    }

    public void AddAccessLevelExemptions(Entity<TurretTargetSettingsComponent> ent, ICollection<ProtoId<AccessLevelPrototype>> exemptions)
    {
        foreach (var exemption in exemptions)
            AddAccessLevelExemption(ent, exemption);
    }

    public void RemoveAccessLevelExemption(Entity<TurretTargetSettingsComponent> ent, ProtoId<AccessLevelPrototype> exemption)
    {
        ent.Comp.ExemptAccessLevels.Remove(exemption);
    }

    public void RemoveAccessLevelExemptions(Entity<TurretTargetSettingsComponent> ent, ICollection<ProtoId<AccessLevelPrototype>> exemptions)
    {
        foreach (var exemption in exemptions)
            RemoveAccessLevelExemption(ent, exemption);
    }

    public void SyncAccessLevelExemptions(Entity<TurretTargetSettingsComponent> source, Entity<TurretTargetSettingsComponent> target)
    {
        target.Comp.ExemptAccessLevels.Clear();
        AddAccessLevelExemptions(target, source.Comp.ExemptAccessLevels);
    }

    public bool HasAccessLevelExemption(Entity<TurretTargetSettingsComponent> ent, ProtoId<AccessLevelPrototype> exemption)
    {
        if (ent.Comp.ExemptAccessLevels.Count == 0)
            return false;

        return ent.Comp.ExemptAccessLevels.Contains(exemption);
    }

    public bool HasAnyAccessLevelExemption(Entity<TurretTargetSettingsComponent> ent, ICollection<ProtoId<AccessLevelPrototype>> exemptions)
    {
        if (ent.Comp.ExemptAccessLevels.Count == 0)
            return false;

        return ent.Comp.ExemptAccessLevels.Any(exemptions.Contains);
    }

    public bool EntityIsTargetForTurret(Entity<TurretTargetSettingsComponent> ent, EntityUid target)
    {
        var accessLevels = _accessReader.FindAccessTags(target);

        return !HasAnyAccessLevelExemption(ent, accessLevels);
    }
}
