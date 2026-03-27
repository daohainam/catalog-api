using EventBus.Events;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace EventBus;
public class IntegrationEventFactory : IIntegrationEventFactory
{
    private static readonly ConcurrentDictionary<string, Type?> typeCache = new();

    public IntegrationEvent? CreateEvent(string typeName, string value)
    {
        var t = GetEventType(typeName) ?? throw new ArgumentException($"Type {typeName} not found");

        return JsonSerializer.Deserialize(value, t) as IntegrationEvent;
    }

    private static Type? GetEventType(string type)
    {
        return typeCache.GetOrAdd(type, static typeName =>
        {
            var t = Type.GetType(typeName);

            return t ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == typeName);
        });
    }

    public static readonly IntegrationEventFactory Instance = new();
}

public class IntegrationEventFactory<TEvent> : IIntegrationEventFactory
{
    private static readonly Assembly integrationEventAssembly = typeof(TEvent).Assembly;
    private static readonly ConcurrentDictionary<string, Type?> typeCache = new();

    public IntegrationEvent? CreateEvent(string typeName, string value)
    {
        var t = GetEventType(typeName) ?? throw new ArgumentException($"Type {typeName} not found");

        return JsonSerializer.Deserialize(value, t) as IntegrationEvent;
    }

    private static Type? GetEventType(string type)
    {
        return typeCache.GetOrAdd(type, static typeName =>
        {
            var t = integrationEventAssembly.GetType(typeName) ?? Type.GetType(typeName);

            return t ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == typeName);
        });
    }

    public static readonly IntegrationEventFactory<TEvent> Instance = new();
}
