using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Dialogue;
using Content.Shared.IdentityManagement;
using Content.Shared.Speech;

namespace Content.Server.Dialogue;

[DataDefinition]
public sealed partial class VendingMachineDialogueTestGoodbyeAction : IDialogueAction
{
    public void PerformAction(Entity<DialogueComponent> ent, EntityUid? user, IEntityManager entityManager)
    {
        if (user == null)
            return;

        if (!entityManager.TryGetComponent<SpeechComponent>(ent, out var speech))
            return;

        var message = Loc.GetString("dialogue-honk-burger-vending-machine-goodbye-bark", ("UserName", Identity.Name(user.Value, entityManager)));
        var chatSystem = entityManager.System<ChatSystem>();

        chatSystem.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, hideChat: true);
    }
}
