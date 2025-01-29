using Content.Server.Access.Systems;
using Content.Server.Hands.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.CriminalRecords;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.StationRecords;
using Content.Shared.Turrets;
using Microsoft.Extensions.DependencyModel;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Goes linearly from 1f to 0f, with 0 damage returning 1f and <see cref=TargetState> damage returning 0f
/// </summary>
public sealed partial class TurretTargetingCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private HandsSystem _hands = default!;
    private AccessReaderSystem _accessReader = default!;
    private IdCardSystem _idCard = default;
    private StationRecordsSystem _records = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        _hands = _entManager.System<HandsSystem>();
        _accessReader = _entManager.System<AccessReaderSystem>();
        _idCard = _entManager.System<IdCardSystem>();
        _records = _entManager.System<StationRecordsSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TurretTargetingComponent>(owner, out var targeting))
            return 1f;

        // If any of the checks are positive, the entity remains a valid target
        // (normally one failed check is sufficent to exempt it from being targeted)

        // Check for cyborgs
        if (targeting.TargetCyborgs && _entManager.HasComponent<BorgChassisComponent>(targetUid))
            return 1f;

        // Check for bots
        if (targeting.TargetBasicSilicons && _entManager.HasComponent<BasicSiliconComponent>(targetUid))
            return 1f;

        // Check for animals
        if (targeting.TargetXenosAndAnimals && _entManager.HasComponent<AnimalComponent>(targetUid))
            return 1f;

        // Check for held contraband
        if (targeting.TargetIsHoldingContraband && _entManager.TryGetComponent<HandsComponent>(targetUid, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld(targetUid, hands))
            {
                if (!_entManager.TryGetComponent<ContrabandComponent>(held, out var contraband))
                    continue;

                // Check if the item is under a blanket ban
                if (contraband.AllowedDepartments.Count == 0 && contraband.AllowedJobs.Count == 0)
                    return 1f;

                // The holder must be wearing their ID to avoid being targeted
                if (!_idCard.TryFindIdCard(targetUid, out var idCard))
                    return 1f;

                // Get the list of jobs were this item can be used
                var allowedJobs = contraband.AllowedJobs;

                foreach (var department in contraband.AllowedDepartments)
                {
                    if (!_protoManager.TryIndex(department, out var prototype))
                        continue;

                    foreach (var job in prototype.Roles)
                        allowedJobs.Add(job);
                }

                // Check is the holder has one of these jobs
                var allowed = false;

                foreach (var job in allowedJobs)
                {
                    if (!_protoManager.TryIndex(job, out var prototype))
                        continue;

                    // Original job titles and changed job titles both count
                    if (prototype.Name == idCard.Comp.JobTitle || prototype.LocalizedName == idCard.Comp.LocalizedJobTitle)
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    return 1f;
            }
        }

        // The following checks only apply to humanoids
        if (_entManager.HasComponent<HumanoidAppearanceComponent>(targetUid))
        {
            // Check criminality status
            if (targeting.TargetWantedCriminals || targeting.TargetCleanCrew)
            {
                var isWanted = TargetIsWantedCriminal(targetUid);

                if (!isWanted && targeting.TargetCleanCrew)
                    return 1f;

                if (isWanted && targeting.TargetWantedCriminals)
                    return 1f;
            }

            // Check for authorized access
            if (targeting.TargetUnauthorizedCrew)
            {
                var idCardAccessLevels = _accessReader.FindAccessTags(targetUid);
                var allowed = false;

                // If the ID card does not have an access level on the authroized list, they will be targeted
                foreach (var accessLevel in idCardAccessLevels)
                {
                    if (targeting.AuthorizedAccessLevels.Contains(accessLevel))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    return 1f;
            }
        }

        return 0f;
    }

    private bool TargetIsWantedCriminal(EntityUid targetUid)
    {
        if (!_idCard.TryFindIdCard(targetUid, out var idCard))
            return false;

        if (!_entManager.TryGetComponent(idCard, out StationRecordKeyStorageComponent? keyStorage) || keyStorage.Key is not { } key)
            return false;

        if (!_records.TryGetRecord<CriminalRecord>(key, out var criminalRecord) || criminalRecord.Status != Shared.Security.SecurityStatus.Wanted)
            return false;

        return true;
    }
}
