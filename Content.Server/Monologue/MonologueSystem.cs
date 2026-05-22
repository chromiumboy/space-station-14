using Content.Server.Chat.Systems;
using Content.Shared.Monologue;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Monologue;

/// <inheritdoc/>
public sealed partial class MonologueSystem : SharedMonologueSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Update(float dt)
    {
        base.Update(dt);

        var query = EntityQueryEnumerator<MonologueComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateMonologue((uid, comp), dt);
        }
    }

    /// <summary>
    /// Causes a monologuing entity to start a pre-defined speech or a supplied one.
    /// </summary>
    /// <param name="ent">The monologuer.</param>
    /// <param name="timeline">A supplied speech, consisting of time stamped lines.</param>
    public void StartMonologue(Entity<MonologueComponent> ent, List<(TimeSpan, MonologueLine)>? timeline = null)
    {
        // Clear the timeline of a previous monologue and reset the timer
        ent.Comp.Timeline.Clear();
        ent.Comp.TimeElapsed = TimeSpan.Zero;

        // If supplied a timeline, use a copy of it for the monologue
        if (timeline != null)
        {
            ent.Comp.Timeline = timeline.ToList();
        }

        // Otherwise try to generate a timeline based on the entity's speech prototype
        else if (_proto.Resolve(ent.Comp.CurrentSpeech, out var proto))
        {
            var tp = TimeSpan.Zero;
            foreach (var line in proto.Speech)
            {
                ent.Comp.Timeline.Add((tp, line));
                tp += line.Delay;
            }
        }

        // If the timeline is empty, pause the monologue to prevent it from being processed in future updates
        ent.Comp.IsPaused = ent.Comp.Timeline.Count == 0;
    }

    /// <summary>
    /// Updates the current speech of a monologuing entity, advancing the timeline
    /// and announcing the last line to have lapsed since the previous update.
    /// </summary>
    /// <param name="ent">The monologuer.</param>
    /// <param name="dt">The time since the last update.</param>
    private void UpdateMonologue(Entity<MonologueComponent> ent, float dt)
    {
        if (ent.Comp.IsPaused)
            return;

        if (ent.Comp.Timeline.Count == 0)
        {
            PauseMonologue(ent, true);
            return;
        }

        ent.Comp.TimeElapsed += TimeSpan.FromSeconds(dt);

        // Determine how many lines have lapsed since the last update
        // and should be removed from the timeline
        var toRemove = 0;

        foreach (var (tp, ev) in ent.Comp.Timeline)
        {
            if (tp > ent.Comp.TimeElapsed)
                break;

            toRemove++;
        }

        if (toRemove == 0)
            return;

        // Announce the most recent line to have lapsed and apply its actions
        // Note that any lines prior to this will skipped entirely
        var current = ent.Comp.Timeline[toRemove - 1].Item2;

        foreach (var action in current.Actions)
        {
            action.PerformAction(ent, EntityManager);
        }

        if (!string.IsNullOrEmpty(current.Line))
        {
            _chat.TrySendInGameICMessage(ent, Loc.GetString(current.Line), current.ChatType, hideChat: ent.Comp.HideFromChat);
        }

        // Remove all elapsed lines from the timeline
        ent.Comp.Timeline.RemoveRange(0, toRemove);
    }

    /// <summary>
    /// Pauses or unpauses the current speech of a monologuing entity.
    /// </summary>
    /// <param name="ent">The monologuer.</param>
    /// <param name="pause">Pause or unpause.</param>
    public void PauseMonologue(Entity<MonologueComponent> ent, bool pause)
    {
        ent.Comp.IsPaused = pause;
    }
}
