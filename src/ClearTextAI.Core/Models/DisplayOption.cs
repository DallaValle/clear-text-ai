namespace ClearTextAI.Core.Models;

/// <summary>An enum value paired with the label shown in a picker.</summary>
public sealed record DisplayOption<T>(T Value, string Name)
{
    public override string ToString() => Name;
}
