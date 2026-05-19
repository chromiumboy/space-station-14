using Content.Shared.Interaction;
using System.Linq;

namespace Content.Shared.Dialogue;

public sealed partial class DialogueSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _userInterfaceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DialogueComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<DialogueComponent, DialogueResponseSelectionMessage>(OnDialogueResponseSelection);
    }

    private void OnActivateInWorld(Entity<DialogueComponent> ent, ref ActivateInWorldEvent ev)
    {
        // Try to find a valid start node
        if (TryMoveToNextNodeInList(ent, ent.Comp.PotentialStartNodes, ev.User))
            return;

        // If no valid start nodes was found, display default text and default response
        var defaultResponse = new Dictionary<int, string> { [-1] = ent.Comp.DefaultResponseText };
        _userInterfaceSystem.SetUiState(ent.Owner, DialogueUiKey.BasicWindow, new DialogueBoundInterfaceState(ent.Comp.DefaultNodeText, defaultResponse));
    }

    private void OnDialogueResponseSelection(Entity<DialogueComponent> ent, ref DialogueResponseSelectionMessage args)
    {
        // Check that the response is valid for the current node, close the dialogue UI if not.
        if (ent.Comp.CurrentNode == null
            || args.ResponseIndex < 0
            || args.ResponseIndex > ent.Comp.CurrentNode.PotentialResponses.Count)
        {
            _userInterfaceSystem.CloseUi(ent.Owner, DialogueUiKey.BasicWindow);
            return;
        }

        var response = ent.Comp.CurrentNode.PotentialResponses[args.ResponseIndex];

        // Try to find a valid node based on the response. Close the dialogue UI if one is not found.
        if (!TryMoveToNextNodeInList(ent, response.PotentialNodes, args.Actor))
        {
            _userInterfaceSystem.CloseUi(ent.Owner, DialogueUiKey.BasicWindow);
        }
    }

    /// <summary>
    /// Finds the next valid dialogue node from the provided list and sends the appropriate data to the client.
    /// </summary>
    /// <param name="ent">The dialogue entity.</param>
    /// <param name="nodeList">The list of dialogue nodes to check.</param>
    /// <param name="user">The user interacting with the dialogue entity.</param>
    /// <returns></returns>
    private bool TryMoveToNextNodeInList(Entity<DialogueComponent> ent, List<DialogueNode> nodeList, EntityUid? user = null)
    {
        // Loop over the potential start nodes
        foreach (var node in nodeList)
        {
            // Check that key whitelists and blacklist are passed
            if (!PassesWhiteList(ent, node.RequiredKeys, user) || !PassesBlackList(ent, node.BlockingKeys, user))
                continue;

            // If the checked are passed, update the component
            AddKeys(ent, node.KeysAdded);
            RemoveKeys(ent, node.KeysRemoved);

            ent.Comp.CurrentNode = node;
            Dirty(ent);

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
                responses = new Dictionary<int, string> { [-1] = ent.Comp.DefaultResponseText };
            }

            // Send data to the client for display
            _userInterfaceSystem.SetUiState(ent.Owner, DialogueUiKey.BasicWindow, new DialogueBoundInterfaceState(node.NodeText, responses));

            return true;
        }

        return false;
    }

    /// <summary>
    /// Tests that all of the keys in the whitelist are attached to the dialogue entity.
    /// If a user is provided, the user's specific keys are also checked.
    /// </summary>
    /// <param name="ent">The dialogue entity.</param>
    /// <param name="whitelist">The whitelisted keys.</param>
    /// <param name="user">The user interacting with the dialogue entity.</param>
    /// <returns></returns>
    private bool PassesWhiteList(Entity<DialogueComponent> ent, List<DialogueKeyPrototype> whitelist, EntityUid? user = null)
    {
        var existingList = ent.Comp.UniversalKeys;

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
    /// <returns></returns>
    private bool PassesBlackList(Entity<DialogueComponent> ent, List<DialogueKeyPrototype> blacklist, EntityUid? user = null)
    {
        var existingList = ent.Comp.UniversalKeys;

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
    public void AddKeys(Entity<DialogueComponent> ent, List<DialogueKeyPrototype> keys, EntityUid? user = null)
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
                userKeys = new HashSet<DialogueKeyPrototype>();
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
    public void RemoveKeys(Entity<DialogueComponent> ent, List<DialogueKeyPrototype> keys, EntityUid? user = null)
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
                userKeys = new HashSet<DialogueKeyPrototype>();
            }

            userKeys.ExceptWith(keys);
            ent.Comp.UserKeys[user.Value] = userKeys;
            Dirty(ent);
        }
    }
}
