using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Marioalexsan.Multitool.Utils;

/// <summary>
/// Extensions for traversing through GameObject hierarchies and applying changes to them.
/// </summary>
public static class HierarchyTraverse
{
    /// <summary>
    /// Executes the given action for the child of the given <see cref="GameObject"/> at the given <paramref name="path"/> relative to the current object.
    /// <para/>
    /// Deeper paths can be specified by using forward slashes. For example <c>child1/child2/target</c> would go through <c>child1 => child2 => target</c> in the hierarchy.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="path">The path where the target child object is located.</param>
    /// <param name="action">The action to execute using the child of the <see cref="GameObject"/>.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    public static GameObject ForChild(this GameObject obj, string path, Action<GameObject> action)
    {
        action(obj.GoToChild(path));

        return obj;
    }

    /// <summary>
    /// Goes to the child of the given <see cref="GameObject"/> at the given <paramref name="path"/> relative to the current object.
    /// <para/>
    /// Deeper paths can be specified by using forward slashes. For example <c>child1/child2/target</c> would go through <c>child1 => child2 => target</c> in the hierarchy.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="path">The path where the target child object is located.</param>
    /// <returns>The target child of the given <see cref="GameObject"/>, for chaining calls.</returns>
    /// <exception cref="ArgumentException">Path is empty.</exception>
    /// <exception cref="InvalidOperationException">Either the child or intemediary nodes don't exist in the given path.</exception>
    public static GameObject GoToChild(this GameObject obj, string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path is empty", nameof(path));

        var parts = path.Split("/");

        if (parts.Length == 0)
            throw new ArgumentException("Path is empty", nameof(path));

        var target = obj;

        for (int i = 0; i < parts.Length; i++)
        {
            var transform = target.transform.Find(parts[i]);

            if (!transform)
                throw new InvalidOperationException($"Child {parts[i]} in path {path} does not exist for object {obj.name}.");

            target = transform.gameObject;
        }

        return target;
    }

    /// <summary>
    /// Executes the given action for each child of the given <see cref="GameObject"/>. The integer parameter indicates the index of the current child.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="action">The action to execute for each child of the given <see cref="GameObject"/>.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    public static GameObject ForEachChild(this GameObject obj, Action<GameObject, int> action)
    {
        var transform = obj.transform;

        for (int i = 0; i < transform.childCount; i++)
        {
            action(transform.GetChild(i).gameObject, i);
        }

        return obj;
    }

    /// <summary>
    /// Executes the given action for the parents of the given <see cref="GameObject"/>. This is the same as calling <see cref="ForParent(GameObject, int, Action{GameObject})"/> with levels equal to 1.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="action">The action to execute using the parent of the <see cref="GameObject"/>.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    /// <exception cref="InvalidOperationException">The current <see cref="GameObject"/> does not have a parent.</exception>
    public static GameObject ForParent(this GameObject obj, Action<GameObject> action)
    {
        action(obj.GoToParent());

        return obj;
    }

    /// <summary>
    /// Executes the given action for the parents of the given <see cref="GameObject"/> situated <paramref name="levels"/> higher in the hierarchy.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="levels">How many levels higher is the target parent compared to the <see cref="GameObject"/>.</param>
    /// <param name="action">The action to execute using the parent of the <see cref="GameObject"/>.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    /// <exception cref="ArgumentException"><paramref name="levels"/> is less than or equal to 0.</exception>
    /// <exception cref="InvalidOperationException">The current <see cref="GameObject"/> does not have a parent at the given level.</exception>
    public static GameObject ForParent(this GameObject obj, int levels, Action<GameObject> action)
    {
        action(obj.GoToParent(levels));

        return obj;
    }

    /// <summary>
    /// Goes to the parent of the given <see cref="GameObject"/>. This is the same as calling <see cref="GoToParent(GameObject, int)"/> with levels equal to 1.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <returns>The parent of the given <see cref="GameObject"/>, for chaining calls.</returns>
    /// <exception cref="InvalidOperationException">The current <see cref="GameObject"/> does not have a parent.</exception>
    public static GameObject GoToParent(this GameObject obj)
        => obj.GoToParent(1);

    /// <summary>
    /// Goes to the parent of the given <see cref="GameObject"/> situated <paramref name="levels"/> higher in the hierarchy.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="levels">How many levels higher is the target parent compared to the <see cref="GameObject"/>.</param>
    /// <returns>The parent of the given <see cref="GameObject"/> at the given level, for chaining calls.</returns>
    /// <exception cref="ArgumentException"><paramref name="levels"/> is less than or equal to 0.</exception>
    /// <exception cref="InvalidOperationException">The current <see cref="GameObject"/> does not have a parent at the given level.</exception>
    public static GameObject GoToParent(this GameObject obj, int levels)
    {
        if (levels <= 0)
            throw new ArgumentException("Levels must be a positive number", nameof(levels));

        int currentLevel = 0;

        while (++currentLevel <= levels)
        {
            var transform = obj.transform.parent;

            if (!transform)
                throw new InvalidOperationException($"Parent at level {currentLevel} does not exist for object {obj.name}.");

            obj = transform.gameObject;
        }

        return obj;
    }

    /// <summary>
    /// Executes the given action for the <see cref="Component"/> of type <typeparamref name="T"/> from the given <see cref="GameObject"/>.
    /// </summary>
    /// <typeparam name="T">The component type you want to retrieve.</typeparam>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="action">The action to execute on the component.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    /// <exception cref="InvalidOperationException">The component does not exist on the given <see cref="GameObject"/>.</exception>
    public static GameObject ForComponent<T>(this GameObject obj, Action<T> action) where T : Component
    {
        var component = obj.GetComponent<T>();

        if (!component)
            throw new InvalidOperationException($"Component {typeof(T).Name} does not exist on object {obj.name}.");

        action(component);

        return obj;
    }

    /// <summary>
    /// Executes the given action for the <see cref="Component"/> of type <typeparamref name="T"/> from the given <see cref="GameObject"/>, if it exists.
    /// <para/>
    /// If the component doesn't exist, this does nothing.
    /// </summary>
    /// <typeparam name="T">The component type you want to retrieve.</typeparam>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="action">The action to execute on the component, if it exists.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    public static GameObject ForComponentIfExists<T>(this GameObject obj, Action<T> action) where T : Component
    {
        var component = obj.GetComponent<T>();

        if (component)
            action(component);

        return obj;
    }

    /// <summary>
    /// Executes the given action for the <see cref="Component"/> of type <typeparamref name="T"/> from the given <see cref="GameObject"/> or any of its children.
    /// </summary>
    /// <typeparam name="T">The component type you want to retrieve.</typeparam>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="action">The action to execute on the component.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    /// <exception cref="InvalidOperationException">The component does not exist on the given <see cref="GameObject"/> or any of its children.</exception>
    public static GameObject ForChildComponent<T>(this GameObject obj, Action<T> action) where T : Component
    {
        var component = obj.GetComponentInChildren<T>();

        if (!component)
            throw new InvalidOperationException($"Component {typeof(T).Name} does not exist on object {obj.name}.");

        action(component);

        return obj;
    }

    /// <summary>
    /// Executes the given action for the <see cref="Component"/> of type <typeparamref name="T"/> from the given <see cref="GameObject"/> or any of its children, if it exists.
    /// <para/>
    /// If the component doesn't exist, this does nothing.
    /// </summary>
    /// <typeparam name="T">The component type you want to retrieve.</typeparam>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="action">The action to execute on the component, if it exists.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    public static GameObject ForChildComponentIfExists<T>(this GameObject obj, Action<T> action) where T : Component
    {
        var component = obj.GetComponentInChildren<T>();

        if (component)
            action(component);

        return obj;
    }

    /// <summary>
    /// Executes the given action for each <see cref="Component"/> of type <typeparamref name="T"/> from the given <see cref="GameObject"/>.
    /// </summary>
    /// <typeparam name="T">The component type you want to retrieve.</typeparam>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="action">The action to execute on each component.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    public static GameObject ForEachComponent<T>(this GameObject obj, Action<T, int> action) where T : Component
    {
        var components = obj.GetComponents<T>();

        for (int i = 0; i < components.Length; i++)
        {
            action(components[i], i);
        }    

        return obj;
    }

    /// <summary>
    /// Gets the <see cref="Component"/> of type <typeparamref name="T"/> from the given <see cref="GameObject"/> and saves a reference to it in <paramref name="field"/>.
    /// </summary>
    /// <typeparam name="T">The component type you want to retrieve.</typeparam>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="field">A field reference to save the component to.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    /// <exception cref="InvalidOperationException">The component does not exist on the given <see cref="GameObject"/>.</exception>
    public static GameObject SaveComponentRef<T>(this GameObject obj, ref T? field) where T : Component
    {
        var component = obj.GetComponent<T>();

        if (!component)
            throw new InvalidOperationException($"Component {typeof(T).Name} does not exist on object {obj.name}.");

        field = component;

        return obj;
    }

    /// <summary>
    /// Renames the given <see cref="GameObject"/>.
    /// </summary>
    /// <param name="obj">The <see cref="GameObject"/> to act upon.</param>
    /// <param name="name">The new name to set.</param>
    /// <returns>The original <see cref="GameObject"/> for chaining calls.</returns>
    public static GameObject Rename(this GameObject obj, string name)
    {
        obj.name = name;
        return obj;
    }
}
