using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse implementation of DragInputProvider using the new Unity Input System.
/// Attach to any GameObject and assign to Draggable.inputProvider.
/// </summary>
public class MouseInputProvider : DragInputProvider
{
    [Header("Camera (leave blank to use Camera.main)")]
    public Camera targetCamera;

    private Camera Cam => targetCamera != null ? targetCamera : Camera.main;

    public override Vector3 PointerWorldPosition
    {
        get
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 wp = Cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            wp.z = 0f;
            return wp;
        }
    }

    public override bool IsPressed => Mouse.current.leftButton.wasPressedThisFrame;
    public override bool IsHeld   => Mouse.current.leftButton.isPressed;
}
