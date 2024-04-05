using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Shared.Containers;

[Virtual]
public class GenericModuleSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenericModuleReceiverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GenericModuleReceiverComponent, AfterInteractUsingEvent>(OnInteractUsing);

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
        TryComp<GenericModuleComponent>(used, out var module);

        if (module != null && CanInsertModule(uid, used, component, module, args.User))
        {
            _container.Insert(used, component.ModuleContainer);
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(args.User):player} installed module {ToPrettyString(used)} into {ToPrettyString(uid)}");
            args.Handled = true;
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

    public bool CanInsertModule(EntityUid uid, EntityUid module, GenericModuleReceiverComponent? component = null, GenericModuleComponent? moduleComponent = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component) || !Resolve(module, ref moduleComponent))
            return false;

        if (component.ModuleContainer.ContainedEntities.Count >= component.MaxModules)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("borg-module-too-many"), uid, user.Value);

            return false;
        }

        if (component.ModuleWhitelist?.IsValid(module, EntityManager) == false)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("borg-module-whitelist-deny"), uid, user.Value);

            return false;
        }

        return true;
    }

    public void InstallModule(EntityUid uid, EntityUid module, GenericModuleReceiverComponent? component, GenericModuleComponent? moduleComponent = null)
    {
        if (!Resolve(uid, ref component) || !Resolve(module, ref moduleComponent))
            return;

        if (moduleComponent.Installed)
            return;

        moduleComponent.InstalledEntity = uid;

        if (moduleComponent.InsertSound != null)
            _audio.PlayPredicted(moduleComponent.InsertSound, uid, module);

        var ev = new GenericModuleInstalledEvent(uid);
        RaiseLocalEvent(module, ref ev);
    }

    public void UninstallModule(EntityUid uid, EntityUid module, GenericModuleReceiverComponent? component, GenericModuleComponent? moduleComponent = null)
    {
        if (!Resolve(uid, ref component) || !Resolve(module, ref moduleComponent))
            return;

        if (!moduleComponent.Installed)
            return;

        moduleComponent.InstalledEntity = null;

        if (moduleComponent.EjectSound != null)
            _audio.PlayPredicted(moduleComponent.EjectSound, uid, module);

        var ev = new GenericModuleUninstalledEvent(uid);
        RaiseLocalEvent(module, ref ev);
    }
}
