namespace Content.Shared.Monologue;

[ImplicitDataDefinitionForInheritors]
public partial interface IMonologueAction
{
    void PerformAction(Entity<MonologueComponent> ent, IEntityManager entityManager);
}
