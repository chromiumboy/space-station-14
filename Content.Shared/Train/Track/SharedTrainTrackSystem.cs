using Content.Shared.Atmos;
using Content.Shared.Popups;
using Content.Shared.Train.Vehicle;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Shared.Train.Track;

/// <summary>
/// Determines in which direction <see cref="TrainVehicleComponent"> entities should move.
/// </summary>
public abstract partial class SharedTrainTrackSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrainTrackComponent, GetTrainVehicleNextDirectionEvent>(OnGetNextDirection);
    }

    private void OnGetNextDirection(Entity<TrainTrackComponent> ent, ref GetTrainVehicleNextDirectionEvent args)
    {
        SelectNextDirection(ent, ref args);
    }

    /// <summary>
    /// Selects the best exit for a train track.
    /// </summary>
    /// <param name="ent">The train track.</param>
    /// <param name="exits">A list of possible exits.</param>
    /// <param name="args">The 'get next direction' event.</param>
    private void SelectNextDirection(Entity<TrainTrackComponent> ent, ref GetTrainVehicleNextDirectionEvent args)
    {
        if (!ent.Comp.Directions.TryGetValue(args.Vehicle.Comp.CurrentDirection, out var exits))
            return;

        switch (exits.Length)
        {
            case 0:
                args.Next = Direction.Invalid;
                return;

            case 1:
                args.Next = exits[1];
                return;

            default:
                args.Next = _random.Pick(exits);
                return;
        }
    }

    /// <summary>
    /// Creates a pop up message over a train track, listing its potential connections.
    /// </summary>
    /// <param name="ent">The train track.</param>
    /// <param name="recipient">The recipient of the pop up message.</param>
    public void PopupDirections(Entity<TrainTrackComponent> ent, EntityUid recipient)
    {
        var exits = ent.Comp.Directions.Keys.Select(x => x.GetOpposite());

        if (exits.Count() == 0)
            return;

        var directions = string.Join(", ", exits);
        _popups.PopupEntity(Loc.GetString("disposal-tube-component-popup-directions-text", ("directions", directions)), ent, recipient);
    }
}
