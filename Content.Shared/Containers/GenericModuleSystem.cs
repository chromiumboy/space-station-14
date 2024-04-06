using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared.Containers;

[Virtual]
public class GenericModuleSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenericModuleReceiverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GenericModuleReceiverComponent, AfterInteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<GenericModuleReceiverComponent, GetVerbsEvent<Verb>>(OnGetVerb);
        SubscribeLocalEvent<GenericModuleReceiverComponent, GetVerbsEvent<ExamineVerb>>(OnExaminableVerb);

        SubscribeLocalEvent<GenericModuleComponent, EntGotInsertedIntoContainerMessage>(OnModuleGotInserted);
        SubscribeLocalEvent<GenericModuleComponent, EntGotRemovedFromContainerMessage>(OnModuleGotRemoved);
    }

    private void OnStartup(EntityUid uid, GenericModuleReceiverComponent component, ComponentStartup args)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containerManager))
            return;

        component.ModuleContainer = _container.EnsureContainer<Container>(uid, component.ModuleContainerId, containerManager);
    }

    private void OnInteractUsing(EntityUid uid, GenericModuleReceiverComponent component, AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled || uid == args.User)
            return;

        var used = args.Used;

        if (!TryComp<GenericModuleComponent>(used, out var module))
            return;

        if (CanInstallModule(uid, used, component, module, args.User))
        {
            _container.Insert(used, component.ModuleContainer);
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(args.User):player} installed module {ToPrettyString(used)} into {ToPrettyString(uid)}");

            args.Handled = true;
        }
    }

    private void OnGetVerb(EntityUid uid, GenericModuleReceiverComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var moduleArray = component.ModuleContainer.ContainedEntities.ToArray();
        Array.Sort(moduleArray, AlphabeticalSort);

        foreach (var ent in component.ModuleContainer.ContainedEntities)
        {
            if (!TryComp<GenericModuleComponent>(ent, out var entModule))
                continue;

            if (!entModule.ManualUninstall)
                continue;

            var verb = new Verb
            {
                Priority = 1,
                Category = VerbCategory.Eject,
                Text = Loc.GetString("generic-module-uninstall", ("module", MetaData(ent).EntityName)),
                Impact = LogImpact.Low,
                DoContactInteraction = true,
                Act = () =>
                {
                    if (CanUninstallModule(uid, ent, component, entModule, args.User))
                        _hands.PickupOrDrop(args.User, ent);
                }
            };

            args.Verbs.Add(verb);
        }
    }

    private void OnModuleGotInserted(EntityUid uid, GenericModuleComponent component, EntGotInsertedIntoContainerMessage args)
    {
        var owner = args.Container.Owner;

        if (!TryComp<GenericModuleReceiverComponent>(owner, out var receiver) ||
            args.Container != receiver.ModuleContainer)
            return;

        InstallModule(owner, uid, receiver, component);
    }

    private void OnModuleGotRemoved(EntityUid uid, GenericModuleComponent component, EntGotRemovedFromContainerMessage args)
    {
        var owner = args.Container.Owner;

        if (!TryComp<GenericModuleReceiverComponent>(owner, out var receiver) ||
            args.Container != receiver.ModuleContainer)
            return;

        UninstallModule(owner, uid, receiver, component);
    }

    private void OnExaminableVerb(EntityUid uid, GenericModuleReceiverComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Examine text should be determined by the system that determines the modules' effects so the text is properly formatted
        var markup = new FormattedMessage();

        RaiseLocalEvent(uid, new GenericModuleReceiverExamineEvent(ref markup));

        if (markup.IsEmpty)
            return;

        markup = FormattedMessage.FromMarkup(markup.ToMarkup().TrimEnd('\n')); // Cursed workaround to https://github.com/space-wizards/RobustToolbox/issues/3371

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                _examine.SendExamineTooltip(args.User, uid, markup, getVerbs: false, centerAtCursor: false);
            },
            Text = Loc.GetString("generic-module-examinable-verb-text"),
            Message = Loc.GetString("generic-module-examinable-verb-message"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    public bool CanInstallModule(EntityUid uid, EntityUid module, GenericModuleReceiverComponent? component = null, GenericModuleComponent? moduleComponent = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component) || !Resolve(module, ref moduleComponent))
            return false;

        if (component.ModuleContainer.ContainedEntities.Count >= component.MaxModules)
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("generic-module-too-many"), uid, user.Value);

            return false;
        }

        if (!component.ModuleWhitelist.IsValid(module, EntityManager))
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("generic-module-whitelist-deny"), uid, user.Value);

            return false;
        }

        if (component.NoDuplicateWhitelistTags && component.ModuleWhitelist.Tags != null)
        {
            IEnumerable<string> unclaimedModuleTags = new List<string>(component.ModuleWhitelist.Tags);

            foreach (var ent in component.ModuleContainer.ContainedEntities)
            {
                if (!TryComp<TagComponent>(ent, out var entTags))
                    continue;

                unclaimedModuleTags = unclaimedModuleTags.Except(entTags.Tags);
            }

            var whitelist = new EntityWhitelist()
            {
                Components = component.ModuleWhitelist.Components,
                Tags = unclaimedModuleTags.ToList(),
                RequireAll = component.ModuleWhitelist.RequireAll
            };

            if (!whitelist.IsValid(module))
            {
                if (user != null)
                    _popup.PopupClient(Loc.GetString("generic-module-whitelist-tag-duplicated"), uid, user.Value);

                return false;
            }
        }

        var ev = new GenericModuleInstallAttemptEvent(uid);
        RaiseLocalEvent(module, ref ev);

        if (ev.Cancelled)
            return false;

        return true;
    }

    public bool CanUninstallModule(EntityUid uid, EntityUid module, GenericModuleReceiverComponent? component = null, GenericModuleComponent? moduleComponent = null, EntityUid? user = null)
    {
        var ev = new GenericModuleUninstallAttemptEvent(uid);
        RaiseLocalEvent(module, ref ev);

        if (ev.Cancelled)
            return false;

        return true;
    }

    public void InstallModule(EntityUid uid, EntityUid module, GenericModuleReceiverComponent? component, GenericModuleComponent? moduleComponent = null)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!Resolve(uid, ref component) || !Resolve(module, ref moduleComponent))
            return;

        if (moduleComponent.Installed)
            return;

        moduleComponent.InstalledEntity = uid;

        if (moduleComponent.InstallSound != null)
            _audio.PlayEntity(moduleComponent.InstallSound, uid, module);

        var ev = new GenericModuleInstalledEvent(uid);
        RaiseLocalEvent(module, ref ev);
    }

    public void UninstallModule(EntityUid uid, EntityUid module, GenericModuleReceiverComponent? component, GenericModuleComponent? moduleComponent = null)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!Resolve(uid, ref component) || !Resolve(module, ref moduleComponent))
            return;

        if (!moduleComponent.Installed)
            return;

        moduleComponent.InstalledEntity = null;

        if (moduleComponent.UninstallSound != null)
            _audio.PlayEntity(moduleComponent.UninstallSound, uid, module);

        var ev = new GenericModuleUninstalledEvent(uid);
        RaiseLocalEvent(module, ref ev);
    }

    private int AlphabeticalSort(EntityUid x, EntityUid y)
    {
        if (string.IsNullOrEmpty(MetaData(x).EntityName))
            return -1;

        if (string.IsNullOrEmpty(MetaData(y).EntityName))
            return 1;

        return MetaData(x).EntityName.CompareTo(MetaData(y).EntityName);
    }
}
