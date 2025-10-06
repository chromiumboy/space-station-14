using Content.Shared.Atmos;
using Content.Shared.Popups;
using Content.Shared.Train.Vehicle;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared.Train.Track;

/// <summary>
/// Handles the basic logic for determining which disposal tubes are connected
/// and which direction entities inside the tubes should move next.
/// </summary>
public sealed partial class TrainTrackSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly SharedTrainVehicleSystem _trainVehicle = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrainTrackComponent, GetTrainVehicleNextDirectionEvent>(OnGetNextDirection);
    }

    private void OnGetNextDirection(Entity<TrainTrackComponent> ent, ref GetTrainVehicleNextDirectionEvent args)
    {
        var exits = GetConnectableDirections(ent);
        SelectNextDirection(ent, exits, ref args);
    }

    /// <summary>
    /// Returns a list of all potential exits to a disposal tube, accounting for its local rotation.
    /// </summary>
    /// <param name="ent">The disposal tube.</param>
    public Direction[] GetConnectableDirections(Entity<TrainTrackComponent> ent)
    {
        var rotation = Transform(ent).LocalRotation;

        return ent.Comp.Directions
            .Select(exit => new Angle(exit.ToAngle() + rotation).GetDir())
            .ToArray();
    }

    /// <summary>
    /// Selects the best exit for a disposal tube, based on a curated list of choices.
    /// </summary>
    /// <param name="ent">The disposal tube.</param>
    /// <param name="exits">The currated list of possible exits from the disposal tube.</param>
    /// <param name="args">The args for the 'get next direction' event.</param>
    public void SelectNextDirection(Entity<TrainTrackComponent> ent, Direction[] exits, ref GetTrainVehicleNextDirectionEvent args)
    {
        if (exits.Length == 0)
            return;

        // The first exit is the default
        args.Next = exits[0];

        // This may change based on the potential exits available
        // and our current direction of travel
        var currentDirection = args.Vehicle.Comp.CurrentDirection;

        switch (exits.Length)
        {
            // There is only one exit
            case 1:
                if (args.Next.GetOpposite() == currentDirection)
                    args.Next = Direction.Invalid;

                return;

            // If there are two exits
            case 2:
                if (args.Next.GetOpposite() == currentDirection)
                    args.Next = exits[1];

                return;

            // If there are more than two exits
            default:

                // Check that the default exit is valid
                if (args.Next.GetOpposite() == currentDirection)
                {
                    // If it isn't, pick one of the remaining exits at random
                    var directions = exits.Skip(1).Where(direction => direction != currentDirection).ToArray();

                    if (directions.Length == 0)
                    {
                        args.Next = Direction.Invalid;
                        return;
                    }

                    args.Next = _random.Pick(directions);
                }

                return;
        }
    }

    /// <summary>
    /// Tries to find an adjacent disposal tube in a specified direction.
    /// </summary>
    /// <param name="ent">The original disposal tube.</param>
    /// <param name="nextDirection">The specified direction.</param>
    /// <returns>The adjacent disposal tube.</returns>
    public EntityUid? NextTubeFor(Entity<TrainTrackComponent> ent, Direction nextDirection)
    {
        var oppositeDirection = nextDirection.GetOpposite();

        var xform = Transform(ent);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        var position = xform.Coordinates;
        foreach (var entity in _map.GetInDir(xform.GridUid.Value, grid, position, nextDirection))
        {
            if (!TryComp(entity, out TrainTrackComponent? tube) || tube.TrainTrackType != ent.Comp.TrainTrackType)
                continue;

            if (!CanConnect((entity, tube), oppositeDirection))
                continue;

            return entity;
        }

        return null;
    }

    /// <summary>
    /// Checks whether a disposal tube can make a valid connection in a specified direction.
    /// </summary>
    /// <param name="ent">The disposal tube.</param>
    /// <param name="direction">The specified direction.</param>
    /// <returns>True if a valid conenction can be made.</returns>
    public bool CanConnect(Entity<TrainTrackComponent> ent, Direction direction)
    {
        if (!Transform(ent).Anchored)
            return false;

        var exits = GetConnectableDirections(ent);

        if (exits.Length == 0)
            return false;

        return exits.Contains(direction);
    }

    /// <summary>
    /// Creates a pop up message over a disposal tube, listing its potential exits.
    /// </summary>
    /// <param name="ent">The disposal tube.</param>
    /// <param name="recipient">The recipient of the pop up message.</param>
    public void PopupDirections(Entity<TrainTrackComponent> ent, EntityUid recipient)
    {
        var exits = GetConnectableDirections(ent);

        if (exits.Length == 0)
            return;

        var directions = string.Join(", ", exits);
        _popups.PopupEntity(Loc.GetString("disposal-tube-component-popup-directions-text", ("directions", directions)), ent, recipient);
    }

    /// <summary>
    /// Tries to insert a collection of entities into the disposals system.
    /// </summary>
    /// <param name="ent">The entry point into disposals.</param>
    /// <param name="toInsert">The entities to insert.</param>
    /// <param name="holderProtoId">The proto ID for the disposal holder.</param>
    /// <param name="holderEnt">The spawned disposals holder.</param>
    /// <param name="tags">Tags to add to the disposed contents.</param>
    /// <returns>True if the insertion was successful.</returns>
    public bool TryInsert
        (Entity<TrainTrackComponent> ent,
        EntityUid[] toInsert,
        EntProtoId holderProtoId,
        [NotNullWhen(true)] out Entity<TrainVehicleComponent>? holderEnt,
        IEnumerable<string>? tags = null)
    {
        holderEnt = null;

        if (toInsert.Length == 0)
            return false;

        if (_net.IsClient && !_timing.IsFirstTimePredicted)
            return false;

        var xform = Transform(ent);
        var holder = Spawn(holderProtoId, _transform.GetMapCoordinates(ent, xform: xform));
        var holderComponent = Comp<TrainVehicleComponent>(holder);
        holderEnt = new Entity<TrainVehicleComponent>(holder, holderComponent);

        if (holderEnt?.Comp.Container == null)
            return false;

        foreach (var entity in toInsert)
        {
            _containerSystem.Insert(entity, holderEnt.Value.Comp.Container);
        }

        return _trainVehicle.TryEnterTrack(holderEnt.Value, ent);
    }
}
