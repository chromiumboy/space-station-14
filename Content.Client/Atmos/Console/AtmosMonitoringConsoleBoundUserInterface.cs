using Content.Shared.Atmos.Components;

namespace Content.Client.Atmos.Console;

public sealed class AtmosMonitoringConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AtmosMonitoringConsoleWindow? _menu;

    public AtmosMonitoringConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        _menu = new AtmosMonitoringConsoleWindow(this, Owner);
        _menu.OpenCentered();
        _menu.OnClose += Close;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        //_menu?.ShowEntites(xform?.Coordinates);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var castState = (AtmosMonitoringConsoleBoundInterfaceState) state;

        if (castState == null)
            return;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        _menu?.ShowEntites(xform?.Coordinates, castState.AirAlarms, castState.FocusData);
    }

    public void SendAtmosMonitoringConsoleMessage(NetEntity? netEntity)
    {
        SendMessage(new AtmosMonitoringConsoleMessage(netEntity));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Dispose();
    }
}
