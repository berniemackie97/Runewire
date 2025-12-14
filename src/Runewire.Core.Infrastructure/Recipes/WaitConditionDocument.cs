namespace Runewire.Core.Infrastructure.Recipes;

internal sealed class WaitConditionDocument
{
    public string? Kind { get; set; }

    public string? Value { get; set; }

    public int? TimeoutMilliseconds { get; set; }
}
