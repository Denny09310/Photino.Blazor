using Microsoft.AspNetCore.Components;

namespace Photino.Blazor;

/// <summary>
/// Represents a collection of <see cref="PhotinoRootComponent"/> entries. This list provides
/// convenience methods for registering root components by type, optionally with parameters. The
/// collection is typically registered as a singleton and injected into the application during
/// initialization.
/// </summary>
public class PhotinoRootComponentsList : List<PhotinoRootComponent>
{
    /// <summary>
    /// Adds a root component of type <typeparamref name="TComponent"/> to the collection. A DOM
    /// selector must be provided to indicate where the component should be rendered.
    /// </summary>
    /// <typeparam name="TComponent">The component type to register. Must implement <see cref="IComponent"/>.</typeparam>
    /// <param name="selector">The CSS selector or registration id of the DOM element.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selector"/> is <c>null</c>.</exception>
    public void Add<TComponent>(string selector) where TComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(selector);
        Add(new PhotinoRootComponent(typeof(TComponent), selector));
    }

    /// <summary>
    /// Adds a root component of the specified type to the collection using the provided selector. This
    /// overload does not include parameters; to specify parameters use <see cref="Add(Type,string,ParameterView)"/>.
    /// </summary>
    /// <param name="componentType">The type of the component to register. Must implement <see cref="IComponent"/>.</param>
    /// <param name="selector">The CSS selector or registration id of the DOM element.</param>
    public void Add(Type componentType, string selector)
    {
        Add(componentType, selector, ParameterView.Empty);
    }

    /// <summary>
    /// Adds a root component of the specified type to the collection with the provided parameters.
    /// </summary>
    /// <param name="componentType">The type of the component to register. Must implement <see cref="IComponent"/>.</param>
    /// <param name="selector">The CSS selector or registration id of the DOM element.</param>
    /// <param name="parameters">The parameters to pass to the root component.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="componentType"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public void Add(Type componentType, string selector, ParameterView parameters)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(selector);
        Add(new PhotinoRootComponent(componentType, selector, parameters));
    }
}