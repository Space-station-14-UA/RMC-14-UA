using System.Linq;

namespace Content.Server._Mriya.NPC.AI;

[DataDefinition]
public sealed partial class AIState
{
    [DataField("facts")]
    public Dictionary<string, string> Facts = new();

    public AIState()
    {
    }

    public AIState(AIState other)
    {
        Facts = new Dictionary<string, string>(other.Facts);
    }

    public AIState Clone() => new(this);

    public bool GetBool(string key)
    {
        return Facts.TryGetValue(key, out var value) && value == AIValues.True;
    }

    public void Set(string key, string value)
    {
        Facts[key] = value;
    }

    public void Set(string key, bool value)
    {
        Facts[key] = AIValues.From(value);
    }

    public bool Matches(AIState desired)
    {
        foreach (var (key, value) in desired.Facts)
        {
            if (!Facts.TryGetValue(key, out var current) || current != value)
                return false;
        }

        return true;
    }

    public AIState Apply(AIAction action)
    {
        var next = new AIState(this);
        foreach (var (key, value) in action.Effects)
        {
            next.Facts[key] = value;
        }

        return next;
    }

    public string Key()
    {
        return string.Join(";", Facts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
    }
}
