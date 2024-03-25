using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;

namespace Content.Client.Pinpointer;

public sealed partial class NavMapSystem : SharedNavMapSystem
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

        component.WallChunks.Clear();

        foreach (var (origin, data) in state.TileData)
        {
            component.WallChunks.Add(origin, new NavMapChunk(origin)
            {
                TileData = data,
            });
        }

        component.Beacons.Clear();
        component.Beacons.AddRange(state.Beacons);

        component.AirlockChunks.Clear();
        component.AirlockChunks.AddRange(state.Airlocks);
    }
}
