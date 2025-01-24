using Robust.Client.UserInterface;

namespace Content.Client.TurretControls;

public sealed class TurretControlsBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private TurretControlsWindow? _window;

    public TurretControlsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        _window = this.CreateWindow<TurretControlsWindow>();
        //_window.SetEntity(Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {

    }

    /*public void SendMessage()
    {
        SendMessage(new Message());
    }*/
}
