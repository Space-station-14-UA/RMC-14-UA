using System.Linq;

namespace Content.Server._Mriya.NPC.AI;

public static class AIPlanner
{
    private const int MaxExpansions = 512;
    private const int MaxDepth = 10;

    private sealed class Node
    {
        public readonly AIState State;
        public readonly float G;
        public readonly Node? Parent;
        public readonly AIAction? Action;
        public readonly int Depth;

        public Node(AIState state, float g, Node? parent, AIAction? action, int depth)
        {
            State = state;
            G = g;
            Parent = parent;
            Action = action;
            Depth = depth;
        }
    }

    public static List<AIAction>? Plan(
        AIGoal goal,
        AIPlannerComponent comp,
        EntityUid owner,
        IEntityManager entMan)
    {
        return Plan(goal.DesiredState, comp, owner, entMan);
    }

    public static List<AIAction>? Plan(
        AIState desired,
        AIPlannerComponent comp,
        EntityUid owner,
        IEntityManager entMan)
    {
        var start = comp.WorldState.Clone();

        if (start.Matches(desired))
            return null;

        var open = new PriorityQueue<Node, float>();
        var bestG = new Dictionary<string, float>(StringComparer.Ordinal);
        var root = new Node(start, 0f, null, null, 0);

        bestG[root.State.Key()] = 0f;
        open.Enqueue(root, Heuristic(root.State, desired));

        var expansions = 0;

        while (open.TryDequeue(out var node, out _))
        {
            if (node.State.Matches(desired))
                return Reconstruct(node);

            if (++expansions > MaxExpansions || node.Depth >= MaxDepth)
                continue;

            foreach (var action in comp.Actions)
            {
                if (!action.PreconditionsMet(node.State))
                    continue;

                if (!action.IsProcedurallyValid(owner, comp, entMan))
                    continue;

                var next = node.State.Apply(action);
                var newG = node.G + MathF.Max(action.Cost, 0.01f);
                var key = next.Key();

                if (bestG.TryGetValue(key, out var oldG) && oldG <= newG)
                    continue;

                bestG[key] = newG;
                var child = new Node(next, newG, node, action, node.Depth + 1);
                open.Enqueue(child, newG + Heuristic(next, desired));
            }
        }

        return null;
    }

    private static float Heuristic(AIState state, AIState desired)
    {
        return 0f;
    }

    private static List<AIAction> Reconstruct(Node node)
    {
        var actions = new List<AIAction>();
        var current = node;

        while (current.Parent != null && current.Action != null)
        {
            actions.Add(current.Action);
            current = current.Parent;
        }

        actions.Reverse();
        return actions;
    }
}
