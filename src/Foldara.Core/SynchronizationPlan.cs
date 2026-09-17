using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Foldara.Core;

/// <summary>
/// Contains an immutable, ordered set of synchronization operations and decisions.
/// </summary>
public sealed class SynchronizationPlan
{
    private static readonly JsonSerializerOptions CompactJsonOptions = CreateJsonOptions(false);
    private static readonly JsonSerializerOptions IndentedJsonOptions = CreateJsonOptions(true);

    public SynchronizationPlan(IEnumerable<SynchronizationOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var copy = new List<SynchronizationOperation>();

        foreach (var operation in operations)
        {
            if (operation is null)
            {
                throw new ArgumentException(
                    "A synchronization plan cannot contain a null operation.",
                    nameof(operations));
            }

            copy.Add(operation);
        }

        Operations = new ReadOnlyCollection<SynchronizationOperation>(copy);
    }

    /// <summary>
    /// Gets operations in deterministic execution and display order.
    /// </summary>
    public IReadOnlyList<SynchronizationOperation> Operations { get; }

    /// <summary>
    /// Serializes the plan to provider-neutral JSON intended for diagnostics and dry runs.
    /// </summary>
    public string ToDiagnosticJson(bool indented = false) =>
        JsonSerializer.Serialize(this, indented ? IndentedJsonOptions : CompactJsonOptions);

    private static JsonSerializerOptions CreateJsonOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = indented,
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new StoragePathJsonConverter());
        return options;
    }

    private sealed class StoragePathJsonConverter : JsonConverter<StoragePath>
    {
        public override StoragePath Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            StoragePath.Parse(reader.GetString() ?? throw new JsonException("A storage path cannot be null."));

        public override void Write(
            Utf8JsonWriter writer,
            StoragePath value,
            JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
