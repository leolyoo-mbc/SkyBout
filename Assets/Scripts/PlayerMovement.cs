using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0.001f), Tooltip("지상 최고 수평 속도 (m/s)")]
    private float maxSpeed = 8f;
    [SerializeField, Min(0.001f), Tooltip("출발과 직진 가속도 (m/s²)")]
    private float acceleration = 10f;
    [SerializeField, Min(0.001f), Tooltip("정지와 초과 속도 감속도 (m/s²)")]
    private float deceleration = 50f;
    [SerializeField, Min(0.001f), Tooltip("이동 중 방향 전환 가속도 (m/s²)")]
    private float turnAcceleration = 50f;
    [SerializeField, Min(0.001f), Tooltip("이륙 지면 대비 점프 높이 (m)")]
    private float jumpHeight = 2f;
    [SerializeField, Tooltip("발 위치가 이 월드 Y 높이보다 낮으면 탈락")]
    private float fallDeathHeight = -10f;
    [SerializeField] private Transform groundCheckTransform;
    [SerializeField, Min(0.001f)] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private readonly ContactPoint[] contactBuffer = new ContactPoint[16];
    private readonly HashSet<Collider> supportingColliders = new HashSet<Collider>();
    private const float SupportTolerance = 0.03f;
    private const float MinSupportNormal = 0.7f;
    private bool reportedContactOverflow;

    private enum PlayerState { Grounded, Airborne, Eliminated }

    private Rigidbody body;
    private PlayerInputActions actions;
    private Vector2 input;
    private PlayerState state = PlayerState.Airborne;
    private bool focused;
    private bool waitingForNeutral;
    private bool pendingJump;
    private bool jumpHeld;
    private bool jumpReadyAfterRelease;
    private GUIStyle eliminationStyle;
    [SerializeField] private TopDownCameraFollow cameraFollow;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool pendingRestart;
    private bool restartHeld;
    private bool restartReadyAfterRelease;

    public bool IsEliminated => state == PlayerState.Eliminated;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        actions = new PlayerInputActions();
        actions.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        startPosition = body.position;
        startRotation = body.rotation;
        ValidateSettings();
    }

    private void OnEnable()
    {
        focused = Application.isFocused;
        waitingForNeutral = true;
        input = Vector2.zero;
        ResetJumpInput();
        ResetRestartInput();
        if (body != null && !body.isKinematic)
            body.WakeUp(); // Rebuild support callbacks after re-enabling a sleeping player.
        if (actions == null)
            return;
        actions.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        SubscribeInput();
        actions.Player.Enable();
    }

    private void OnDisable()
    {
        UnsubscribeInput();
        if (actions != null)
            actions.Player.Disable();
        waitingForNeutral = true;
        supportingColliders.Clear();
        if (state != PlayerState.Eliminated)
            state = PlayerState.Airborne;
        ResetJumpInput();
        ResetRestartInput();
        StopHorizontalMovement();
    }

    private void OnDestroy()
    {
        UnsubscribeInput();
        if (actions != null)
        {
            actions.Disable();
            actions.Dispose();
            actions = null;
        }
    }

    private void SubscribeInput()
    {
        if (actions == null)
            return;
        actions.Player.Move.performed += OnMovePerformed;
        actions.Player.Move.canceled += OnMoveCanceled;
        actions.Player.Jump.performed += OnJumpPerformed;
        actions.Player.Jump.canceled += OnJumpCanceled;
        actions.Player.Restart.performed += OnRestartPerformed;
        actions.Player.Restart.canceled += OnRestartCanceled;
        InputSystem.onAfterUpdate += CheckForNeutralInput;
    }

    private void UnsubscribeInput()
    {
        if (actions != null)
        {
            actions.Player.Move.performed -= OnMovePerformed;
            actions.Player.Move.canceled -= OnMoveCanceled;
            actions.Player.Jump.performed -= OnJumpPerformed;
            actions.Player.Jump.canceled -= OnJumpCanceled;
            actions.Player.Restart.performed -= OnRestartPerformed;
            actions.Player.Restart.canceled -= OnRestartCanceled;
        }
        InputSystem.onAfterUpdate -= CheckForNeutralInput;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        input = isActiveAndEnabled && state != PlayerState.Eliminated && focused && !waitingForNeutral
            ? Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f)
            : Vector2.zero;
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        input = Vector2.zero;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context) => HandleJump(true);

    private void OnJumpCanceled(InputAction.CallbackContext context) => HandleJump(false);

    private void HandleJump(bool pressed)
    {
        if (state == PlayerState.Eliminated)
            return;
        bool newPress = pressed && !jumpHeld;
        jumpHeld = pressed;
        if (!isActiveAndEnabled || !focused)
            return;
        if (!pressed)
            return;
        if (newPress && jumpReadyAfterRelease && HasGroundSupport())
            pendingJump = true;
    }

    private void OnRestartPerformed(InputAction.CallbackContext context) => HandleRestart(true);

    private void OnRestartCanceled(InputAction.CallbackContext context) => HandleRestart(false);

    private void HandleRestart(bool pressed)
    {
        bool newPress = pressed && !restartHeld;
        restartHeld = pressed;
        if (newPress && restartReadyAfterRelease && isActiveAndEnabled && focused && state == PlayerState.Eliminated)
        {
            pendingRestart = true;
            restartReadyAfterRelease = false;
        }
    }

    private void ResetRestartInput()
    {
        pendingRestart = false;
        restartHeld = false;
        restartReadyAfterRelease = false;
    }

    private void Restart()
    {
        pendingRestart = false;
        body.position = startPosition;
        body.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, startRotation);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        supportingColliders.Clear();
        state = PlayerState.Airborne;
        ResetJumpInput();
        waitingForNeutral = false;
        input = Vector2.ClampMagnitude(actions.Player.Move.ReadValue<Vector2>(), 1f);
        body.WakeUp();
        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    private void ResetJumpInput()
    {
        pendingJump = false;
        jumpHeld = false;
        jumpReadyAfterRelease = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        waitingForNeutral = true;
        ResetJumpInput();
        ResetRestartInput();
        StopHorizontalMovement();
    }

    private void CheckForNeutralInput()
    {
        if (!focused || actions == null)
            return;
        var restart = actions.Player.Restart;
        if (restart.enabled)
        {
            bool actuated = false;
            foreach (var control in restart.controls)
                actuated |= control.IsActuated();
            if (!actuated)
            {
                restartHeld = false;
                restartReadyAfterRelease = true;
            }
        }
        if (state == PlayerState.Eliminated)
            return;
        var jump = actions.Player.Jump;
        // Button release is observed here as well, independent of callback delivery.
        if (jump.enabled)
        {
            bool actuated = false;
            foreach (var control in jump.controls)
                actuated |= control.IsActuated();
            if (!actuated)
            {
                jumpHeld = false;
                jumpReadyAfterRelease = true;
            }
        }
        if (!waitingForNeutral)
            return;
        var move = actions.Player.Move;
        if (!move.enabled)
            return;
        // Check after device synchronization, including opposing keys whose vector cancels out.
        foreach (var control in move.controls)
            if (control.IsActuated())
                return;
        waitingForNeutral = false;
    }

    private void FixedUpdate()
    {
        if (state != PlayerState.Eliminated && FootPosition.y < fallDeathHeight)
        {
            state = PlayerState.Eliminated;
            pendingRestart = false;
            supportingColliders.Clear();
            ResetJumpInput();
            StopHorizontalMovement();
            return;
        }
        switch ((state, pendingRestart, focused))
        {
            case (PlayerState.Eliminated, true, true):
                Restart();
                return;
            case (PlayerState.Eliminated, _, _):
                StopHorizontalMovement();
                return;
        }
        state = HasGroundSupport() ? PlayerState.Grounded : PlayerState.Airborne;
        bool launch = pendingJump;
        pendingJump = false;
        switch ((state, launch, focused))
        {
            case (PlayerState.Grounded, true, true):
                // Compensate the half-step loss of semi-implicit gravity integration.
                float gravity = -Physics.gravity.y;
                float speed = Mathf.Sqrt(2f * gravity * jumpHeight) + gravity * Time.fixedDeltaTime * 0.5f;
                body.linearVelocity = new Vector3(body.linearVelocity.x, speed, body.linearVelocity.z);
                state = PlayerState.Airborne;
                break;
        }
        if (!focused || waitingForNeutral)
        {
            StopHorizontalMovement();
            return;
        }

        Vector3 current = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        Vector3 target = direction * maxSpeed;
        float rate = direction switch
        {
            var value when value.sqrMagnitude < 0.0001f => deceleration,
            var value when current.sqrMagnitude > 0.0001f
                && Vector3.Dot(current.normalized, value.normalized) < 0.95f => turnAcceleration,
            _ when current.magnitude > target.magnitude => deceleration,
            _ => acceleration
        };
        Vector3 next = Vector3.MoveTowards(current, target, rate * Time.fixedDeltaTime);
        body.linearVelocity = new Vector3(next.x, body.linearVelocity.y, next.z);
    }

    // Contact callbacks provide actual support, not merely a nearby collider.
    private void OnCollisionEnter(Collision collision) => RefreshSupport(collision);
    private void OnCollisionStay(Collision collision) => RefreshSupport(collision);
    private void OnCollisionExit(Collision collision) => supportingColliders.Remove(collision.collider);

    private Vector3 FootPosition => body.position + (groundCheckTransform.position - transform.position);

    private void RefreshSupport(Collision collision)
    {
        // Unity also delivers collision messages to disabled MonoBehaviours.
        if (state == PlayerState.Eliminated || !isActiveAndEnabled || groundCheckTransform == null)
            return;
        supportingColliders.Remove(collision.collider);
        if ((groundLayer.value & (1 << collision.gameObject.layer)) == 0)
            return;
        int count = collision.GetContacts(contactBuffer);
        if (count == contactBuffer.Length && !reportedContactOverflow)
        {
            reportedContactOverflow = true;
            Debug.LogError("PlayerMovement: ground contact buffer is full; review collider complexity.", this);
        }
        Vector3 foot = FootPosition;
        for (int i = 0; i < count; i++)
        {
            ContactPoint contact = contactBuffer[i];
            if (contact.normal.y >= MinSupportNormal && contact.separation <= SupportTolerance
                && Mathf.Abs(contact.point.y - foot.y) <= SupportTolerance)
            {
                supportingColliders.Add(collision.collider);
                return;
            }
        }
    }

    private bool HasGroundSupport()
    {
        if (state == PlayerState.Eliminated || body.linearVelocity.y > 0.1f || supportingColliders.Count == 0)
            return false;
        // Recheck current geometry: cached contacts alone must not grant coyote time.
        Vector3 foot = FootPosition;
        return Physics.Raycast(foot + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit,
            0.05f + SupportTolerance, groundLayer, QueryTriggerInteraction.Ignore)
            && hit.normal.y >= MinSupportNormal && supportingColliders.Contains(hit.collider);
    }

    private void StopHorizontalMovement()
    {
        input = Vector2.zero;
        if (body != null && !body.isKinematic)
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying && body != null)
            ValidateSettings();
    }

    private void ValidateSettings()
    {
        int ground = LayerMask.NameToLayer("Ground");
        if (actions != null && groundCheckTransform != null && groundCheckTransform != transform
            && groundCheckTransform.IsChildOf(transform) && groundLayer.value != 0
            && ground >= 0 && (groundLayer.value & (1 << ground)) != 0
            && (groundLayer.value & (1 << gameObject.layer)) == 0
            && Positive(maxSpeed) && Positive(acceleration) && Positive(deceleration)
            && Positive(turnAcceleration) && Positive(groundCheckRadius) && Positive(jumpHeight)
            && !float.IsNaN(fallDeathHeight) && !float.IsInfinity(fallDeathHeight)
            && body != null && !body.isKinematic
            && body.useGravity && body.linearDamping == 0f
            && (body.constraints & RigidbodyConstraints.FreezePositionY) == 0
            && Positive(-Physics.gravity.y) && Physics.gravity.x == 0f && Physics.gravity.z == 0f)
            return;
        Debug.LogError("PlayerMovement: check input actions, child ground probe, Ground mask, dynamic gravity Rigidbody (zero damping, Y unlocked), downward gravity and finite positive movement/jump values plus a finite fall death height.", this);
        enabled = false;
    }

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);

    private void OnGUI()
    {
        if (state != PlayerState.Eliminated)
            return;
        // shortcut: 로컬 1인 프로토타입 안내, 실제 HUD·다국어 도입 시 UI 체계로 이전.
        if (eliminationStyle == null)
            eliminationStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        eliminationStyle.fontSize = Mathf.Clamp(Screen.width / 25, 18, 32);
        float width = Mathf.Min(400f, Screen.width * 0.9f);
        var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - 96f) * 0.5f, width, 96f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.95f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(panel, "ELIMINATED\nR: 재시작", eliminationStyle);
        GUI.color = previousColor;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckTransform == null)
            return;
        Gizmos.color = state == PlayerState.Grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheckTransform.position, groundCheckRadius);
    }
}
