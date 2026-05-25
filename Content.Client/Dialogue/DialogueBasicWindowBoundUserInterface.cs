using Content.Shared.Dialogue;
using Robust.Client.UserInterface;
using System.Numerics;

namespace Content.Client.Dialogue;

public sealed class DialogueBasicWindowBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DialogueBasicWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DialogueBasicWindow>();
        _window.SetOwner(Owner);
        _window.OpenCenteredAt(new Vector2(0.5f, 0.75f));

        _window.OnResponseSelectedEvent += OnResponseSelected;
    }

    private void OnResponseSelected(int responseIndex)
    {
        SendPredictedMessage(new DialogueResponseSelectionMessage(responseIndex));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not DialogueBasicWindowBoundInterfaceState { } castState)
            return;

        _window?.UpdateState(castState);
    }
}
