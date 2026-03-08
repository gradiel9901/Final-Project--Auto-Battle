using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Config")]
    public float flySpeed = 20f;
    public float sprintMultiplier = 3f;
    
    [Header("Look Config")]
    public float mouseSensitivity = 0.2f; // Reduced significantly for new input system delta
    private float pitch = 0f;
    private float yaw = 0f;

    [Header("Time Config")]
    public float timeStep = 0.5f; // How much speed changes per key press
    private float currentTimeScale = 1.0f;
    private readonly float maxTimeScale = 8.0f;

    private void Start()
    {
        // Lock cursor to center for freecam look
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Initialize rotation
        Vector3 rot = transform.localRotation.eulerAngles;
        yaw = rot.y;
        pitch = rot.x;

        // Reset time
        Time.timeScale = 1.0f;
    }

    private void Update()
    {
        if (Keyboard.current == null) return; // Prevent errors if no keyboard

        // Optional: Press Escape to unlock mouse so you can click the Restart button if victory happens
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        HandleMouseLook();
        HandleMovement();
        HandleTimeScale();
    }

    private void HandleMouseLook()
    {
        // Only allow look if cursor is locked
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -89f, 89f); // Prevent flipping upside down

        transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
    }

    private void HandleMovement()
    {
        float speed = flySpeed;
        if (Keyboard.current.leftShiftKey.isPressed) speed *= sprintMultiplier;

        float moveX = 0f;
        float moveZ = 0f;
        float moveY = 0f;

        if (Keyboard.current.aKey.isPressed) moveX = -1f;
        if (Keyboard.current.dKey.isPressed) moveX = 1f;
        
        if (Keyboard.current.wKey.isPressed) moveZ = 1f;
        if (Keyboard.current.sKey.isPressed) moveZ = -1f;

        if (Keyboard.current.eKey.isPressed || Keyboard.current.spaceKey.isPressed) moveY = 1f;   // Ascend
        if (Keyboard.current.cKey.isPressed) moveY = -1f;  // Descend

        Vector3 move = transform.right * moveX + transform.up * moveY + transform.forward * moveZ;
        
        // Apply movement scaled by unscaled delta time so camera speed isn't affected by time manipulation
        transform.position += move * speed * Time.unscaledDeltaTime; 
    }

    private void HandleTimeScale()
    {
        // R to increase speed
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            currentTimeScale += timeStep;
            if (currentTimeScale > maxTimeScale) currentTimeScale = maxTimeScale;
            
            Time.timeScale = currentTimeScale;
            Debug.Log("Time Scale: " + currentTimeScale + "x");
        }

        // Q to decrease speed 
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            currentTimeScale -= timeStep;
            
            // Unity's native Time.timeScale CANNOT be negative. It strictly throws an error.
            if (currentTimeScale < 0f) currentTimeScale = 0f;
            
            Time.timeScale = currentTimeScale;
            Debug.Log("Time Scale: " + currentTimeScale + "x");
        }
        
        // Reset to normal time with T
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            currentTimeScale = 1.0f;
            Time.timeScale = currentTimeScale;
            Debug.Log("Time Scale Reset: 1.0x");
        }
    }
}
