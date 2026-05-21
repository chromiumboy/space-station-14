using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared.Dialogue;

/// <summary>
/// System for handling dialogue interactions. This includes managing dialogue trees,
/// tracking player progress through dialogue, and sending dialogue data to the client UI.
/// </summary>
public sealed partial class DialogueSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DialogueComponent, OpenBoundInterfaceMessage>(OnBoundUIOpened);
        SubscribeLocalEvent<DialogueComponent, DialogueResponseSelectionMessage>(OnDialogueResponseSelection);

        _sawmill = _logManager.GetSawmill("DialogueSystem");
    }

    private void OnBoundUIOpened(Entity<DialogueComponent> ent, ref OpenBoundInterfaceMessage ev)
    {
        if (!_protoManager.TryIndex(ent.Comp.CurrentTree, out DialogueTreePrototype? proto))
            return;

        // Move to the start node
        TryMoveToNode(ent, ev.Actor, proto, proto.Start);
    }

    private void OnDialogueResponseSelection(Entity<DialogueComponent> ent, ref DialogueResponseSelectionMessage args)
    {
        if (!_protoManager.TryIndex(ent.Comp.CurrentTree, out DialogueTreePrototype? proto))
            return;

        // Check that the response is valid for the current node. Close the dialogue window if this fails.
        if (ent.Comp.UserCurrentNodes == null
            || !ent.Comp.UserCurrentNodes.TryGetValue(args.Actor, out var currentNodeName)
            || !proto.Nodes.TryGetValue(currentNodeName, out var currentNode)
            || args.ResponseIndex < 0
            || args.ResponseIndex > currentNode.PotentialResponses.Count)
        {
            _userInterfaceSystem.CloseUi(ent.Owner, DialogueUiKey.BasicWindow, args.Actor);
            return;
        }

        // Add/remove keys as required
        var response = currentNode.PotentialResponses[args.ResponseIndex];

        AddKeys(ent, response.KeysAdded, args.Actor);
        RemoveKeys(ent, response.KeysRemoved, args.Actor);

        // Perform response actions
        foreach (var action in response.Actions)
        {
            action.PerformAction(ent, args.Actor, EntityManager);
        }

        // Try to move to the connected node based on the response. Close the dialogue window if this fails.
        if (!TryMoveToNode(ent, args.Actor, proto, response.NextNode))
        {
            _userInterfaceSystem.CloseUi(ent.Owner, DialogueUiKey.BasicWindow, args.Actor);
        }
    }

    /// <summary>
    /// Moves the dialogue to a specified node and sends the appropriate data to the client UI.
    /// </summary>
    /// <param name="ent">The dialogue entity.</param>
    /// <param name="user">The user interacting with the dialogue entity.</param>
    /// <param name="proto">The dialogue tree prototype for the entity.</param>
    /// <param name="nodeName">The name of the new node.</param>
    /// <returns>True if the transition was successful.</returns>
    private bool TryMoveToNode(Entity<DialogueComponent> ent, EntityUid user, DialogueTreePrototype? proto = null, string? nodeName = null)
    {
        if (nodeName == null)
            return false;

        if (proto == null && !_protoManager.TryIndex(ent.Comp.CurrentTree, out proto))
            return false;

        if (!proto.Nodes.TryGetValue(nodeName, out var node))
            return false;

        // Add/remove keys as required
        AddKeys(ent, node.KeysAdded, user);
        RemoveKeys(ent, node.KeysRemoved, user);

        // Update the current node
        ent.Comp.UserCurrentNodes[user] = node.Name;
        Dirty(ent);

        // Perform node actions
        foreach (var action in node.Actions)
        {
            action.PerformAction(ent, user, EntityManager);
        }

        // Determine potential responses to the node
        var responses = new Dictionary<int, string>();

        for (int idx = 0; idx < node.PotentialResponses.Count; idx++)
        {
            var response = node.PotentialResponses[idx];

            if (!PassesWhiteList(ent, response.RequiredKeys, user) || !PassesBlackList(ent, response.BlockingKeys, user))
                continue;

            responses.Add(idx, response.ResponseText);
        }

        // If no responses pass, set the default response
        if (responses.Count == 0)
        {
            responses = new Dictionary<int, string> { [-1] = "dialogue-tree-response-text-undefined" };
            _sawmill.Error($"Dialogue tree node [{proto.ID} - {nodeName}] has no valid response.");
        }

        // Update the display (but only from the client side)
        if (_netManager.IsClient)
        {
            _userInterfaceSystem.SetUiState(ent.Owner, DialogueUiKey.BasicWindow, new DialogueBasicWindowBoundInterfaceState(node.NodeText, responses));
        }

        return true;
    }

    /// <summary>
    /// Tests that all of the keys in the whitelist are attached to the dialogue entity.
    /// If a user is provided, the user's specific keys are also checked.
    /// </summary>
    /// <param name="ent">The dialogue entity.</param>
    /// <param name="whitelist">The whitelisted keys.</param>
    /// <param name="user">The user interacting with the dialogue entity.</param>
    /// <returns>True if the test passed.</returns>
    private bool PassesWhiteList(Entity<DialogueComponent> ent, List<string> whitelist, EntityUid? user = null)
    {
        var existingList = ent.Comp.UniversalKeys.ToHashSet();

        if (user != null && ent.Comp.UserKeys.TryGetValue(user.Value, out var userKeys))
        {
            existingList.UnionWith(userKeys);
        }

        return whitelist.All(existingList.Contains);
    }

    /// <summary>
    /// Tests that none of the keys in the blacklist are attached to the dialogue entity.
    /// If a player is provided, the user's specific keys are also checked.
    /// </summary>
    /// <param name="ent">The dialogue entity.</param>
    /// <param name="blacklist">The blacklisted keys.</param>
    /// <param name="user">The user interacting with the dialogue entity.</param>
    /// <returns>True if the test passed.</returns>
    private bool PassesBlackList(Entity<DialogueComponent> ent, List<string> blacklist, EntityUid? user = null)
    {
        var existingList = ent.Comp.UniversalKeys.ToHashSet();

        if (user != null && ent.Comp.UserKeys.TryGetValue(user.Value, out var userKeys))
        {
            existingList.UnionWith(userKeys);
        }

        return !existingList.Any(blacklist.Contains);
    }

    /// <summary>
    /// Adds the specific keys to the dialogue entity. If a user is provided, the keys are added to the user's specific keys.
    /// Otherwise they are added to the universal key list.
    /// </summary>
    /// <param name="ent">The dialogue entity.</param>
    /// <param name="keys">The keys to add.</param>
    /// <param name="user">The user interacting with the dialogue entity.</param>
    public void AddKeys(Entity<DialogueComponent> ent, List<string> keys, EntityUid? user = null)
    {
        if (user == null)
        {
            ent.Comp.UniversalKeys.UnionWith(keys);
            Dirty(ent);
        }
        else
        {
            if (!ent.Comp.UserKeys.TryGetValue(user.Value, out var userKeys))
            {
                userKeys = new HashSet<string>();
            }

            userKeys.UnionWith(keys);
            ent.Comp.UserKeys[user.Value] = userKeys;
            Dirty(ent);
        }
    }

    /// <summary>
    /// Removes the specific keys to the dialogue entity. If a user is provided, the keys are removed from the user's specific keys.
    /// Otherwise they are removed from the universal key list.
    /// </summary>
    /// <param name="ent">The dialogue entity.</param>
    /// <param name="keys">The keys to remove.</param>
    /// <param name="user">The user interacting with the dialogue entity.</param>
    public void RemoveKeys(Entity<DialogueComponent> ent, List<string> keys, EntityUid? user = null)
    {
        if (user == null)
        {
            ent.Comp.UniversalKeys.ExceptWith(keys);
            Dirty(ent);
        }
        else
        {
            if (!ent.Comp.UserKeys.TryGetValue(user.Value, out var userKeys))
            {
                userKeys = new HashSet<string>();
            }

            userKeys.ExceptWith(keys);
            ent.Comp.UserKeys[user.Value] = userKeys;
            Dirty(ent);
        }
    }
}
