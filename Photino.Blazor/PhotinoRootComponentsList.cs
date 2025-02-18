using Microsoft.AspNetCore.Components;

namespace Photino.Blazor;

/// <summary>
/// Defines a collection of <see cref="PhotinoRootComponent"/> items.
/// </summary>
public class PhotinoRootComponentsList : List<PhotinoRootComponent>
{
    /// <summary>
    /// Adds a component mapping to the collection.
    /// </summary>
    /// <typeparam name="TComponent">The component type.</typeparam>
    /// <param name="selector">The DOM element selector.</param>
    /// <exception cref="ArgumentNullException">Occurs when <paramref name="selector"/> is null.</exception>
    public void Add<TComponent>(string selector) where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(selector);

        Add(new PhotinoRootComponent(typeof(TComponent), selector));
    }

    /// <summary>
    /// Adds a component mapping to the collection.
    /// </summary>
    /// <param name="componentType">The component type. Must implement <see cref="IComponent"/>.</param>
    /// <param name="selector">The DOM element selector.</param>
    public void Add(Type componentType, string selector)
    {
        Add(componentType, selector, ParameterView.Empty);
    }

    /// <summary>
    /// Adds a component mapping to the collection.
    /// </summary>
    /// <param name="componentType">The component type. Must implement <see cref="IComponent"/>.</param>
    /// <param name="selector">The DOM element selector.</param>
    /// <param name="parameters">The parameters to the root component.</param>
    /// <exception cref="ArgumentNullException">Occurs when <paramref name="componentType"/> or <paramref name="selector"/> is null.</exception>
    public void Add(Type componentType, string selector, ParameterView parameters)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        ArgumentNullException.ThrowIfNull(selector);

        Add(new PhotinoRootComponent(componentType, selector, parameters));
    }
}