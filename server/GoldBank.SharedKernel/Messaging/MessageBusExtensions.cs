using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GoldBank.SharedKernel.Messaging;

/// <summary>
/// Extension methods for registering the in-process message bus and auto-discovering message handlers.
/// When WolverineFx adds .NET 10 support, these registrations will be replaced by Wolverine's
/// built-in host configuration (app.UseWolverine()).
/// </summary>
public static class MessageBusExtensions
{
    /// <summary>
    /// Registers the in-process message bus as a singleton and auto-discovers all
    /// <see cref="IMessageHandler{T}"/> implementations from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for message handler implementations.</param>
    public static IServiceCollection AddInProcessMessaging(this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Register the message bus as a singleton (it owns the background channel processor)
        services.TryAddSingleton<IMessageBus, InMemoryMessageBus>();

        // Auto-discover and register all IMessageHandler<T> implementations
        foreach (var assembly in assemblies)
        {
            RegisterHandlersFromAssembly(services, assembly);
        }

        return services;
    }

    /// <summary>
    /// Registers the in-process message bus and scans the calling assembly for handlers.
    /// </summary>
    public static IServiceCollection AddInProcessMessaging(this IServiceCollection services)
    {
        return services.AddInProcessMessaging(Assembly.GetCallingAssembly());
    }

    private static void RegisterHandlersFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaceType = typeof(IMessageHandler<>);

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType)
                .Select(i => new { ImplementationType = t, ServiceType = i }))
            .ToList();

        foreach (var handler in handlerTypes)
        {
            // Register as scoped so handlers can inject scoped services (e.g., DbContext)
            services.AddScoped(handler.ServiceType, handler.ImplementationType);
        }
    }
}
