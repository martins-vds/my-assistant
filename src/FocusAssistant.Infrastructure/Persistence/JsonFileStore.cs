using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace FocusAssistant.Infrastructure.Persistence;

/// <summary>
/// Generic file persistence helper that reads/writes collections of entities as JSON.
/// Uses atomic writes (write-to-temp then rename) to prevent data corruption.
/// </summary>
public sealed class JsonFileStore<T> where T : class
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { EnablePrivateSetters }
        }
    };

    public JsonFileStore(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        EnsureDirectoryExists();
    }

    public async Task<List<T>> ReadAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
                return new List<T>();

            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return new List<T>();

            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WriteAllAsync(List<T> items, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(items, JsonOptions);

            // Atomic write: write to temp file then rename
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> ReadSingleAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = await File.ReadAllTextAsync(_filePath, ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WriteSingleAsync(T item, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(item, JsonOptions);

            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool Exists() => File.Exists(_filePath);

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Type info modifier that enables deserialization into properties with private setters.
    /// This keeps domain entities clean (no [JsonInclude] attributes) while allowing STJ to populate them.
    /// </summary>
    private static void EnablePrivateSetters(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var prop in typeInfo.Properties)
        {
            if (prop.Set is not null)
                continue;

            if (prop.AttributeProvider is PropertyInfo clrProperty)
            {
                var setter = clrProperty.GetSetMethod(nonPublic: true);
                if (setter is not null)
                {
                    prop.Set = (obj, value) => clrProperty.SetValue(obj, value);
                }
            }
        }
    }
}
