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
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {

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
