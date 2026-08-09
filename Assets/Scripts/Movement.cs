using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [Header("SETTINGS")]
    [SerializeField] private float MovementSpeed = 6.0f;
    [SerializeField] private float RotationSpeed = 2.0f;
    [SerializeField] private float LookSensitivity = 1.0f;
    [SerializeField] private bool LockCursor = true;

    [Header("REFERENCES")]
    [SerializeField] private Rigidbody PlayerRb;
    [SerializeField] private Transform CameraTransform;

    [Header("INPUT")]
    [SerializeField] private InputActionReference MoveActionRef;
    [SerializeField] private InputActionReference LookActionRef;

    private float cameraPitch = 0f;

    private void Awake()
    {
        // Freeze Rigidbody rotation so physics doesn't tilt or tip the player
        PlayerRb.freezeRotation = true;
    }

    private void Start()
    {
        if (LockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        MoveActionRef.action.Enable();
        LookActionRef.action.Enable();
    }

    private void Update()
    {
        HandleCameraLook();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private Vector2 GetMoveInput()
    {
        Vector2 input = MoveActionRef.action.ReadValue<Vector2>();
        return Vector2.ClampMagnitude(input, 1f);
    }

    private Vector2 GetLookInput()
    {
        return LookActionRef.action.ReadValue<Vector2>();
    }

    private void HandleCameraLook()
    {
        Vector2 lookInput = GetLookInput();

        // Horizontal rotation around Y axis
        float yaw = lookInput.x * RotationSpeed * LookSensitivity;
        transform.Rotate(Vector3.up * yaw);

        // Vertical pitch around X axis for camera
        cameraPitch -= lookInput.y * RotationSpeed * LookSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);
        CameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 moveInput = GetMoveInput();
        Vector3 moveDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

        Vector3 targetVelocity = moveDir * MovementSpeed;
        Vector3 currentVelocity = PlayerRb.linearVelocity;

        // Apply velocity change along horizontal plane (preserve Y gravity)
        Vector3 velocityChange = new Vector3(targetVelocity.x - currentVelocity.x, 0f, targetVelocity.z - currentVelocity.z);
        PlayerRb.AddForce(velocityChange, ForceMode.VelocityChange);
    }
}