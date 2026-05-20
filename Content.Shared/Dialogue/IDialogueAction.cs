namespace Content.Shared.Dialogue;

[ImplicitDataDefinitionForInheritors]
public partial interface IDialogueAction
{
    void PerformAction(Entity<DialogueComponent> ent, EntityUid? user, IEntityManager entityManager);
}
