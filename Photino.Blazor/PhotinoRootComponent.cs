using Microsoft.AspNetCore.Components;

namespace Photino.Blazor;

/// <summary>
/// Associates a Blazor root component type with a target selector in the DOM. Instances of
/// <see cref="PhotinoRootComponent"/> are created via constructors that perform validation on the
/// provided <see cref="Type"/> and selector values to ensure correct usage.
/// </summary>
public readonly struct PhotinoRootComponent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhotinoRootComponent"/> struct with the provided
    /// component type and selector. The component type must implement <see cref="IComponent"/> and
    /// both parameters must be non-null.
    /// </summary>
    /// <param name="componentType">The type of the root component to render.</param>
    /// <param name="selector">The CSS selector or registration id for the DOM element.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="componentType"/> or <paramref name="selector"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="componentType"/> does not implement <see cref="IComponent"/>.</exception>
    public PhotinoRootComponent(Type componentType, string selector)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(selector);

        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"The type '{componentType.Name}' must implement {nameof(IComponent)} to be used as a root component.", nameof(componentType));
        }

        ComponentType = componentType;
        Selector = selector;
        Parameters = ParameterView.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotinoRootComponent"/> struct with the provided
    /// component type, selector, and component parameters. This overload delegates to the other
    /// constructor and then sets the <see cref="Parameters"/> property.
    /// </summary>
    /// <param name="componentType">The type of the root component to render.</param>
    /// <param name="selector">The CSS selector or registration id for the DOM element.</param>
    /// <param name="parameters">A <see cref="ParameterView"/> containing parameters to pass to the root component.</param>
    public PhotinoRootComponent(Type componentType, string selector, ParameterView parameters) : this(componentType, selector)
    {
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the component type to render.
    /// </summary>
    public Type ComponentType { get; }

    /// <summary>
    /// Gets the parameters passed to the root component.
    /// </summary>
    public ParameterView Parameters { get; }

    /// <summary>
    /// Gets the CSS selector or registration id of the DOM element where the component will be rendered.
    /// </summary>
    public string Selector { get; }
}