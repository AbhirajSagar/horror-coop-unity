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

    [Header("GROUND CHECK")]
    [SerializeField] private float GroundCheckDistance = 1.2f;
    [SerializeField] private LayerMask GroundMask = ~0;

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

        // Apply friction-less material to prevent capsule wall-sticking and edge popping
        Collider col = GetComponent<Collider>();
        if (col != null && col.sharedMaterial == null)
        {
            PhysicsMaterial noFriction = new PhysicsMaterial("PlayerNoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            col.material = noFriction;
        }
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
        Vector3 rawMoveDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

        // Check if player is grounded
        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, GroundCheckDistance, GroundMask);

        Vector3 moveDir = rawMoveDir;
        if (isGrounded)
        {
            // Project movement along ground slope normal to prevent step launching
            moveDir = Vector3.ProjectOnPlane(rawMoveDir, hit.normal).normalized;
        }

        Vector3 targetVelocity = moveDir * MovementSpeed;
        Vector3 currentVelocity = PlayerRb.linearVelocity;

        if (isGrounded)
        {
            // Clamp upward Y velocity spike caused by hitting bumps while grounded
            float currentY = currentVelocity.y;
            if (currentY > 0f)
            {
                currentY = 0f;
            }

            PlayerRb.linearVelocity = new Vector3(targetVelocity.x, currentY, targetVelocity.z);
        }
        else
        {
            // Preserve gravity Y velocity in air
            Vector3 velocityChange = new Vector3(targetVelocity.x - currentVelocity.x, 0f, targetVelocity.z - currentVelocity.z);
            PlayerRb.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }
}