using EventBus.Events;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace EventBus;

/// <summary>
/// Resolves an event type name that arrived over the wire against a bounded set of
/// known event types. The lookup is deliberately restricted to a single assembly:
/// resolving arbitrary type names from message payloads is a deserialization
/// gadget surface, and scanning every loaded assembly is both slow and dependent
/// on which assemblies happen to be loaded at the time.
/// </summary>
public class IntegrationEventFactory<TEvent> : IIntegrationEventFactory
{
    private static readonly ConcurrentDictionary<string, Type?> typeCache = BuildTypeCache();

    public IntegrationEvent? CreateEvent(string typeName, string value)
    {
        var t = GetEventType(typeName);
        if (t == null)
        {
            // Returning null (rather than throwing) is what callers expect: the
            // consumer logs the unknown type and moves on instead of crashing.
            return null;
        }

        return JsonSerializer.Deserialize(value, t) as IntegrationEvent;
    }

    internal static Type? GetEventType(string typeName) =>
        typeCache.TryGetValue(typeName, out var type) ? type : null;

    private static ConcurrentDictionary<string, Type?> BuildTypeCache()
    {
        var cache = new ConcurrentDictionary<string, Type?>(StringComparer.Ordinal);

        foreach (var type in GetEventTypes(typeof(TEvent).Assembly))
        {
            if (type.FullName is { } fullName)
            {
                cache[fullName] = type;
            }
        }

        return cache;
    }

    private static IEnumerable<Type> GetEventTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.OfType<Type>()];
        }

        return types.Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IntegrationEvent).IsAssignableFrom(t));
    }

    public static readonly IntegrationEventFactory<TEvent> Instance = new();
}
