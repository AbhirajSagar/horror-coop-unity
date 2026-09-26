using System;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : NetworkBehaviour
{
    [Header("SETTINGS")]
    [SerializeField] private float MovementSpeed = 6.0f;
    [SerializeField] private float RotationSpeed = 2.0f;
    [SerializeField] private float LookSensitivity = 1.0f;
    [SerializeField] private bool LockCursor = true;

    [Header("CROUCH SETTINGS")]
    [SerializeField] private float CrouchYScale = 0.5f;
    [SerializeField] private float CrouchSpeedMultiplier = 0.6f;
    [SerializeField] private float CrouchTransitionSpeed = 10f;

    [Header("ANIMATIONS")]
    [SerializeField] private Animator PlayerAnimator;

    [Header("GROUND CHECK")]
    [SerializeField] private float GroundCheckDistance = 1.2f;
    [SerializeField] private LayerMask GroundMask = ~0;

    [Header("REFERENCES")]
    [SerializeField] private Rigidbody PlayerRb;
    [SerializeField] private Transform CameraTransform;

    [Header("INPUT")]
    [SerializeField] private InputActionReference MoveActionRef;
    [SerializeField] private InputActionReference LookActionRef;
    [SerializeField] private InputActionReference CrouchActionRef;

    [Header("FOOTSTEPS")]
    [SerializeField] private AudioClip[] FootstepSounds;
    [SerializeField] private float FootstepDistance = 1.8f;

    [Header("HEADBOB SETTINGS")]
    [SerializeField] private bool EnableHeadbob = true;
    [SerializeField] private float BobFrequency = 10f;
    [SerializeField] private float BobHorizontalAmount = 0.025f;
    [SerializeField] private float BobVerticalAmount = 0.035f;
    [SerializeField] private float BobCrouchMultiplier = 0.6f;
    [SerializeField] private float BobSmoothSpeed = 10f;

    [Header("AUDIO")]
    [SerializeField] private AudioSource AudioPlayer;
    [SerializeField] private AudioListener Listener;

    private float cameraPitch = 0f;
    private float originalYScale = 1f;
    private bool isCrouching = false;
    private bool isGrounded = false;
    private Vector3 defaultCameraLocalPos;
    private float bobTimer = 0f;
    private readonly int XHash = Animator.StringToHash("x");
    private readonly int YHash = Animator.StringToHash("y");
    private Vector3 lastFootstepPosition;
    

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            lastFootstepPosition = transform.position;
            return;
        }

        CameraTransform.gameObject.SetActive(false);
        Listener.enabled = false;
    }

    private void Start()
    {
        originalYScale = transform.localScale.y;
        if (CameraTransform != null)
        {
            defaultCameraLocalPos = CameraTransform.localPosition;
        }

        if (!IsOwner && IsSpawned)
        {
            if (CameraTransform != null) CameraTransform.gameObject.SetActive(false);
            return;
        }

        if (LockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (MoveActionRef != null && MoveActionRef.action != null) MoveActionRef.action.Enable();
        if (LookActionRef != null && LookActionRef.action != null) LookActionRef.action.Enable();
        if (CrouchActionRef != null && CrouchActionRef.action != null) CrouchActionRef.action.Enable();
    }

    private void Update()
    {
        if (IsSpawned && !IsOwner) return;
        HandleCameraLook();
        HandleCrouch();
        HandleHeadbob();
    }

    private void HandleAnimations(Vector2 inputMovement)
    {
        PlayerAnimator.SetFloat(XHash, inputMovement.x);
        PlayerAnimator.SetFloat(YHash, inputMovement.y);
    }

    private void FixedUpdate()
    {
        if (IsSpawned && !IsOwner) return;
        HandleMovement();
    }

    private Vector2 GetMoveInput()
    {
        if (MoveActionRef == null || MoveActionRef.action == null) return Vector2.zero;
        Vector2 input = MoveActionRef.action.ReadValue<Vector2>();
        return Vector2.ClampMagnitude(input, 1f);
    }

    private Vector2 GetLookInput()
    {
        if (LookActionRef == null || LookActionRef.action == null) return Vector2.zero;
        return LookActionRef.action.ReadValue<Vector2>();
    }

    private void HandleCrouch()
    {
        if (CrouchActionRef == null || CrouchActionRef.action == null) return;
        isCrouching = CrouchActionRef.action.IsPressed();

        float targetYScale = isCrouching ? originalYScale * CrouchYScale : originalYScale;
        Vector3 targetScale = new Vector3(transform.localScale.x, targetYScale, transform.localScale.z);

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * CrouchTransitionSpeed);
    }

    private void HandleHeadbob()
    {
        if (CameraTransform == null) return;

        Vector2 moveInput = GetMoveInput();
        bool isMoving = EnableHeadbob && isGrounded && moveInput.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            float speedMultiplier = isCrouching ? BobCrouchMultiplier : 1f;
            bobTimer += Time.deltaTime * (BobFrequency * speedMultiplier);

            float horizontalBob = Mathf.Cos(bobTimer) * (BobHorizontalAmount * speedMultiplier);
            float verticalBob = Mathf.Sin(bobTimer * 2f) * (BobVerticalAmount * speedMultiplier);

            Vector3 targetLocalPos = defaultCameraLocalPos + new Vector3(horizontalBob, verticalBob, 0f);
            CameraTransform.localPosition = Vector3.Lerp(CameraTransform.localPosition, targetLocalPos, Time.deltaTime * BobSmoothSpeed);
        }
        else
        {
            bobTimer = 0f;
            CameraTransform.localPosition = Vector3.Lerp(CameraTransform.localPosition, defaultCameraLocalPos, Time.deltaTime * BobSmoothSpeed);
        }
    }

    private void HandleCameraLook()
    {
        if (CameraTransform == null) return;
        Vector2 lookInput = GetLookInput();

        float yaw = lookInput.x * RotationSpeed * LookSensitivity;
        transform.Rotate(Vector3.up * yaw);

        cameraPitch -= lookInput.y * RotationSpeed * LookSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);
        CameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 moveInput = GetMoveInput();
        HandleAnimations(moveInput);
        Vector3 rawMoveDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;

        float currentGroundCheckDist = GroundCheckDistance * (transform.localScale.y / originalYScale);
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, currentGroundCheckDist, GroundMask);

        Vector3 moveDir = rawMoveDir;
        if (isGrounded) moveDir = Vector3.ProjectOnPlane(rawMoveDir, hit.normal).normalized;

        float speed = isCrouching ? MovementSpeed * CrouchSpeedMultiplier : MovementSpeed;
        Vector3 targetVelocity = moveDir * speed;
        Vector3 currentVelocity = PlayerRb.linearVelocity;

        if (isGrounded)
        {
            float currentY = currentVelocity.y > 0f ? 0f : currentVelocity.y;
            PlayerRb.linearVelocity = new Vector3(targetVelocity.x, currentY, targetVelocity.z);
        }
        else
        {
            Vector3 velocityChange = new(targetVelocity.x - currentVelocity.x, 0f, targetVelocity.z - currentVelocity.z);
            PlayerRb.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        if (isGrounded && moveInput.sqrMagnitude > 0.01f)
        {
            float distance = Vector3.Distance(transform.position, lastFootstepPosition);

            if (distance >= FootstepDistance)
            {
                PlayAudioStepSound();
                lastFootstepPosition = transform.position;
            }
        }
    }

    public void PlayAudioStepSound()
    {
        AudioPlayer.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
        AudioPlayer.PlayOneShot(FootstepSounds[UnityEngine.Random.Range(0, FootstepSounds.Length)]);
    }
}