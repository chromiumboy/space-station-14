using Robust.Shared.GameStates;

namespace Content.Shared.Pinpointer;

/// <summary>
/// This is used for objects which appear as doors on the navmap.
/// </summary>
[Obsolete]
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedNavMapSystem))]
public sealed partial class NavMapDoorComponent : Component
{

}
