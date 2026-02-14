using UnityEngine;

/// <summary>
/// Abstract input source for the drag system.
/// Swap implementations to support mouse, touch, or controller without
/// changing any drag logic.
/// </summary>
public abstract class DragInputProvider : MonoBehaviour
{
    /// <summary>World-space position of the pointer this frame.</summary>
    public abstract Vector3 PointerWorldPosition { get; }

    /// <summary>True on the first frame the primary action button is pressed.</summary>
    public abstract bool IsPressed { get; }

    /// <summary>True every frame the primary action button is held down.</summary>
    public abstract bool IsHeld { get; }
}
