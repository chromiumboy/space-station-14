using Content.Shared.Access;
using Content.Shared.TurretController;
using Content.Shared.Turrets;
using Robust.Client.UserInterface;

namespace Content.Client.TurretController;

public sealed class TurretControllerWindowBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private TurretControllerWindow? _window;

    public TurretControllerWindowBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        if (UiKey is not DeployableTurretControllerUiKey)
        {
            Close();
            return;
        }

        _window = this.CreateWindow<TurretControllerWindow>();
        _window.SetOwnerAndUiKey(Owner, (DeployableTurretControllerUiKey)UiKey);
        _window.OpenCentered();

        _window.OnAccessLevelChangedEvent += OnAccessLevelChanged;
        _window.OnArmamentSettingChangedEvent += OnArmamentSettingChanged;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null)
            return;

        if (state is not DeployableTurretControllerWindowBoundInterfaceState { } castState)
            return;

        _window.UpdateState(castState);
    }

    private void OnAccessLevelChanged(AccessLevelPrototype accessLevel, bool enabled)
    {
        SendMessage(new DeployableTurretExemptAccessLevelChangedMessage(accessLevel, enabled));
    }

    private void OnArmamentSettingChanged(int setting)
    {
        SendMessage(new DeployableTurretArmamentSettingChangedMessage(setting));
    }
}
