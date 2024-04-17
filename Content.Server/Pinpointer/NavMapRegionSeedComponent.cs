using Robust.Shared.GameStates;

namespace Content.Server.Pinpointer;

/// <summary>
/// Used to mark entities that are seeds for generating nav map regions on client UI
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NavMapRegionSeedComponent : Component
{

}
