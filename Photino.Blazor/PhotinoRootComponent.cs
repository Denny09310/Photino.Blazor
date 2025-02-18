using Microsoft.AspNetCore.Components;

namespace Photino.Blazor;

/// <summary>
/// Defines a mapping between a root <see cref="IComponent"/> and a DOM element selector.
/// </summary>
public readonly struct PhotinoRootComponent
{
    /// <summary>
    /// Gets the component type.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets the DOM element selector.
    /// </summary>
    public string Selector { get; }

    /// <summary>
    /// Gets the parameters to pass to the root component.
    /// </summary>
    public ParameterView Parameters { get; }

    /// <summary>
    /// Creates a new instance of <see cref="PhotinoRootComponent"/> with the provided <paramref name="componentType"/>
    /// and <paramref name="selector"/>.
    /// </summary>
    /// <param name="componentType">The component type. Must implement <see cref="IComponent"/>.</param>
    /// <param name="selector">The DOM element selector or component registration id for the component.</param>
    /// <exception cref="ArgumentNullException">Occurs when the <paramref name="componentType"/> or <paramref name="selector"/> parameters are null.</exception>
    /// <exception cref="ArgumentException">Occurs when <paramref name="componentType"/> does not inherit from <see cref="IComponent"/>.</exception>
    public PhotinoRootComponent(Type componentType, string selector)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"The type '{componentType.Name}' must implement {nameof(IComponent)} to be used as a root component.", nameof(componentType));
        }

        ArgumentNullException.ThrowIfNull(selector);

        ComponentType = componentType;
        Selector = selector;
        Parameters = ParameterView.Empty;
    }

    /// <summary>
    /// Creates a new instance of <see cref="PhotinoRootComponent"/> with the provided <paramref name="componentType"/>,
    /// <paramref name="selector"/>, and <paramref name="parameters"/>.
    /// </summary>
    /// <param name="componentType">The component type. Must implement <see cref="IComponent"/>.</param>
    /// <param name="selector">The DOM element selector or component registration id for the component.</param>
    /// <param name="parameters">The parameters to pass to the component,</param>
    public PhotinoRootComponent(Type componentType, string selector, ParameterView parameters) : this(componentType, selector)
    {
        Parameters = parameters;
    }
}
