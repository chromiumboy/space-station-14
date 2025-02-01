using Content.Shared.Access;
using Content.Shared.TurretControls;
using Content.Shared.Turrets;
using Robust.Client.UserInterface;

namespace Content.Client.TurretControls;

public sealed class TurretControlsBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private TurretControlsWindow? _window;

    public TurretControlsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        if (UiKey is not TurretControlsUiKey)
        {
            Close();
            return;
        }

        _window = this.CreateWindow<TurretControlsWindow>();
        _window.SetOwnerAndUiKey(Owner, (TurretControlsUiKey)UiKey);
        _window.OpenCentered();

        _window.OnAccessLevelChangedEvent += OnAccessLevelChanged;
        _window.OnArmamentSettingChangedEvent += OnArmamentSettingChanged;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null)
            return;

        if (state is not TurretControlsBoundInterfaceState { } castState)
            return;

        _window.RefreshLinkedTurrets(castState.TurretStates);
    }

    private void OnAccessLevelChanged(AccessLevelPrototype accessLevel, bool enabled)
    {
        SendMessage(new TurretControlAccessLevelChangedMessage(accessLevel, enabled));
    }

    private void OnArmamentSettingChanged(TurretControlsArmamentState setting)
    {
        SendMessage(new TurretControlArmamentSettingChangedMessage(setting));
    }

    public void SendTurretControlsUpdatedMessage(Entity<TurretTargetingComponent> ent)
    {
        /*var ev = new TurretControlSettingsChangedMessage()
        {
            ent.Comp.
        }

        SendMessage((netEntity, group));*/
    }
}
