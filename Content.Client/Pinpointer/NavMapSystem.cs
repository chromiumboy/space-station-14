using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;

namespace Content.Client.Pinpointer;

public sealed class NavMapSystem : SharedNavMapSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NavMapComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, NavMapComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not NavMapComponentState state)
            return;

        component.Chunks.Clear();

        foreach (var (origin, data) in state.TileData)
        {
            component.Chunks.Add(origin, new NavMapChunk(origin)
            {
                TileData = data,
            });
        }

        component.Beacons.Clear();
        component.Beacons.AddRange(state.Beacons);

        component.Airlocks.Clear();
        component.Airlocks.AddRange(state.Airlocks);
    }
}
