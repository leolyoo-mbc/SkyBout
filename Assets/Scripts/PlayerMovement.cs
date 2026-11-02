using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    private enum PlayerState { Grounded, Airborne, Gliding, Eliminated }

    // Inspector 설정: 이동·점프·낙사
    [FormerlySerializedAs("maxSpeed")]
    [SerializeField, Min(0.001f), Tooltip("지상·공중 공통 최고 수평 속도 (m/s)")]
    private float maxHorizontalSpeed = 8f;
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
    [SerializeField, Tooltip("글라이딩 목표 발 위치의 월드 Y 높이 (m). 낙사 높이보다 높아야 함")]
    private float glideTargetFootHeight = 2f;
    [SerializeField, Min(0.001f), Tooltip("목표 높이 접근과 감쇠의 응답 계수 (1/s)")]
    private float glideHeightResponse = 3f;
    [SerializeField, Range(0.001f, 10f), Tooltip("글라이딩 수직 보정의 최대 가속도 (m/s²)")]
    private float glideVerticalAcceleration = 10f;

    // Inspector 참조: 발 위치·지면 레이어·카메라
    [FormerlySerializedAs("groundCheckTransform")]
    [SerializeField, Tooltip("캐릭터의 발바닥 위치")]
    private Transform feetTransform;
    [FormerlySerializedAs("groundCheckRadius")]
    [SerializeField, Min(0.001f), Tooltip("발 위치를 표시하는 디버그 구의 반경. 접지 판정에는 사용하지 않음")]
    private float feetMarkerRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private TopDownCameraFollow cameraFollow;
    [SerializeField, Tooltip("충돌 없는 플레이어 자식 날개 표시")]
    private GameObject gliderVisual;
    [SerializeField, Tooltip("온라인 prefab은 소유권 확정 전 입력과 물리를 실행하지 않음")]
    private bool requiresLocalBinding;
    private bool localBinding;
    private bool controlSuspended;

    // 컴포넌트 참조와 현재 지상·공중·탈락 상태
    private Rigidbody playerRigidbody;
    private PlayerInputActions playerInput;
    private PlayerInputReader inputReader;
    private readonly GroundSupport groundSupport = new GroundSupport();
    private readonly PlayerEliminationOverlay eliminationOverlay = new PlayerEliminationOverlay();
    private PlayerState state = PlayerState.Airborne;

    // 입력 시점에 허용된 요청을 다음 물리 갱신에서 한 번만 실행한다.
    private bool jumpRequested;
    private bool glideRequested;
    private bool restartRequested;

    // 재시작할 최초 자세
    private Vector3 startingPosition;
    private Quaternion startingRotation;

    public bool IsEliminated => state == PlayerState.Eliminated;
    public bool IsGliding => state == PlayerState.Gliding;
    public bool HasLocalControl => !requiresLocalBinding || localBinding;
    public int MotionState => (int)state;
    public event System.Action Restarted;
    private Vector3 FootPosition => playerRigidbody.position + (feetTransform.position - transform.position);

    // ── Unity 생명주기와 물리 갱신 순서 ──

    private void Awake() // 생성 시 참조와 시작 자세를 준비한다.
    {
        playerRigidbody = GetComponent<Rigidbody>();
        playerInput = new PlayerInputActions();
        playerInput.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        inputReader = new PlayerInputReader(playerInput);
        inputReader.JumpPressed += RequestJump;
        inputReader.JumpReleased += CancelGlide;
        inputReader.RestartPressed += RequestRestart;
        startingPosition = playerRigidbody.position;
        startingRotation = playerRigidbody.rotation;
        if (HasLocalControl)
            ValidateSettings();
        else
            playerRigidbody.isKinematic = true;
        UpdateGliderVisual();
    }

    private void OnEnable() // 활성화 시 입력을 연결한다.
    {
        ResetRequests();
        if (playerRigidbody != null && !playerRigidbody.isKinematic)
            playerRigidbody.WakeUp(); // 재활성화된 플레이어의 지면 접촉 콜백을 다시 받는다.
        if (HasLocalControl && !controlSuspended)
            inputReader?.Enable(Application.isFocused);
    }

    private void OnDisable() // 비활성화 시 입력과 지면 접촉 정보를 정리한다.
    {
        inputReader?.Disable();
        groundSupport.Clear();
        if (!IsEliminated && !IsGliding)
            state = PlayerState.Airborne;
        ResetRequests();
        StopHorizontalMovement();
    }

    private void OnDestroy() // 삭제 시 입력 자산을 해제한다.
    {
        if (playerRigidbody != null)
            playerRigidbody.useGravity = true;
        if (gliderVisual != null)
            gliderVisual.SetActive(false);
        if (inputReader != null)
        {
            inputReader.Disable();
            inputReader.JumpPressed -= RequestJump;
            inputReader.JumpReleased -= CancelGlide;
            inputReader.RestartPressed -= RequestRestart;
        }
        if (playerInput != null)
        {
            playerInput.Disable();
            playerInput.Dispose();
            playerInput = null;
        }
    }

    private void OnApplicationFocus(bool hasFocus) // 창 초점이 바뀌면 이전 입력을 버린다.
    {
        if (!HasLocalControl)
            return;
        inputReader?.SetFocus(hasFocus);
        ResetRequests();
        StopHorizontalMovement();
    }

    private void FixedUpdate() // 일정한 물리 간격으로 플레이어 동작을 처리한다.
    {
        if (!HasLocalControl)
            return;
        // 새로 낙사한 틱에는 재시작·점프·이동을 처리하지 않는다.
        if (!IsEliminated && FootPosition.y < fallDeathHeight)
        {
            EliminatePlayer();
            return;
        }

        // 탈락 상태에서는 초점이 있을 때 새 재시작 요청만 실행한다.
        if (IsEliminated)
        {
            if (restartRequested && inputReader.HasFocus)
                RestartAtStartingPosition();
            else
                StopHorizontalMovement();

            return;
        }

        UpdateGroundedState();
        ProcessJumpRequest();
        ProcessGlideRequest();
        if (IsGliding && !inputReader.IsJumpHeld)
            CancelGlide();
        if (IsGliding)
            ApplyGlideVelocity();

        // 초점·이동 키 해제 대기는 수평 이동만 막는다. 공중 물리는 계속된다.
        if (!inputReader.CanMove)
        {
            StopHorizontalMovement();
            return;
        }

        UpdateHorizontalVelocity();
    }

    // ── 입력 시점의 게임 규칙 확인 ──

    private void RequestJump()
    {
        // 입력 순간의 의도를 고정한다. 한 틱의 추가 입력으로 점프/전개를 연쇄 실행하지 않는다.
        if (!isActiveAndEnabled || IsEliminated || jumpRequested || glideRequested)
            return;
        if (IsStandingOnGround())
            jumpRequested = true;
        else if (!IsGliding)
            glideRequested = true;
    }

    private void RequestRestart()
    {
        if (isActiveAndEnabled && IsEliminated)
            restartRequested = true;
    }

    private void CancelGlide()
    {
        glideRequested = false;
        if (IsGliding)
            SetState(PlayerState.Airborne);
    }

    private void ResetRequests()
    {
        jumpRequested = false;
        glideRequested = false;
        restartRequested = false;
    }

    // ── 탈락과 시작 위치 복귀 ──

    private void EliminatePlayer()
    {
        SetState(PlayerState.Eliminated);
        ResetRequests();
        groundSupport.Clear();
        inputReader.SetEliminated(true);
        StopHorizontalMovement();
    }

    private void RestartAtStartingPosition()
    {
        ResetRequests();
        playerRigidbody.position = startingPosition;
        playerRigidbody.rotation = startingRotation;
        transform.SetPositionAndRotation(startingPosition, startingRotation);
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
        groundSupport.Clear();
        SetState(PlayerState.Airborne);
        inputReader.SetEliminated(false);
        playerRigidbody.WakeUp();
        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
        Restarted?.Invoke();
    }

    // ── 생존 중 접지·점프·수평 속도 갱신 ──

    private void UpdateGroundedState()
    {
        if (IsStandingOnGround())
        {
            SetState(PlayerState.Grounded);
            glideRequested = false;
        }
        else if (!IsGliding)
            SetState(PlayerState.Airborne);
    }

    private void ProcessJumpRequest()
    {
        bool shouldJump = jumpRequested;

        // 실행할 수 없는 요청도 이번 물리 갱신에서 버린다.
        jumpRequested = false;

        if (!shouldJump || !inputReader.HasFocus || state != PlayerState.Grounded)
            return;

        ApplyJumpVelocity();
        SetState(PlayerState.Airborne);
    }

    private void ProcessGlideRequest()
    {
        bool shouldGlide = glideRequested;
        glideRequested = false;
        if (shouldGlide && inputReader.IsJumpHeld && state == PlayerState.Airborne)
            SetState(PlayerState.Gliding);
    }

    private void ApplyGlideVelocity()
    {
        float verticalVelocity = MovementMath.CalculateGlideVerticalVelocity(FootPosition.y, glideTargetFootHeight,
            playerRigidbody.linearVelocity.y, glideHeightResponse, glideVerticalAcceleration, Time.fixedDeltaTime);
        playerRigidbody.linearVelocity = new Vector3(playerRigidbody.linearVelocity.x,
            verticalVelocity, playerRigidbody.linearVelocity.z);
    }

    private void SetState(PlayerState nextState)
    {
        state = nextState;
        playerRigidbody.useGravity = !IsGliding;
        UpdateGliderVisual();
    }

    private void UpdateGliderVisual()
    {
        if (gliderVisual != null && gliderVisual.activeSelf != IsGliding)
            gliderVisual.SetActive(IsGliding);
    }

    private void ApplyJumpVelocity()
    {
        float jumpSpeed = MovementMath.CalculateJumpSpeed(jumpHeight, -Physics.gravity.y, Time.fixedDeltaTime);
        playerRigidbody.linearVelocity = new Vector3(playerRigidbody.linearVelocity.x, jumpSpeed, playerRigidbody.linearVelocity.z);
    }

    private void UpdateHorizontalVelocity()
    {
        playerRigidbody.linearVelocity = MovementMath.CalculateVelocity(playerRigidbody.linearVelocity, inputReader.Move,
            maxHorizontalSpeed, acceleration, deceleration, turnAcceleration, Time.fixedDeltaTime);
    }

    private void StopHorizontalMovement()
    {
        inputReader?.ClearMovement();
        if (playerRigidbody != null && !playerRigidbody.isKinematic)
            playerRigidbody.linearVelocity = new Vector3(0f, playerRigidbody.linearVelocity.y, 0f);
    }

    // ── 발밑 지면 접촉 수집과 현재 위치 재검사 ──

    private void OnCollisionEnter(Collision collision) => UpdateGroundContacts(collision);
    private void OnCollisionStay(Collision collision) => UpdateGroundContacts(collision);
    private void OnCollisionExit(Collision collision) => groundSupport.Remove(collision.collider);

    private void UpdateGroundContacts(Collision collision)
    {
        // Unity는 비활성 MonoBehaviour에도 충돌 메시지를 보낼 수 있다.
        if (!HasLocalControl || state == PlayerState.Eliminated || !isActiveAndEnabled || feetTransform == null)
            return;
        groundSupport.UpdateContacts(collision, FootPosition, groundLayer, this);
        // 물리 충돌이 해결된 첫 표시 프레임부터 날개를 닫는다.
        if (IsGliding && IsStandingOnGround())
            UpdateGroundedState();
    }

    private bool IsStandingOnGround()
    {
        return !IsEliminated && groundSupport.IsStanding(FootPosition, playerRigidbody.linearVelocity.y, groundLayer);
    }

    // ── 설정 검증 ──

    private void OnValidate() // 실행 중 Inspector 값이 바뀌면 설정을 재검사한다.
    {
        if (Application.isPlaying && playerRigidbody != null && HasLocalControl)
            ValidateSettings();
    }

    private void ValidateSettings()
    {
        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        bool groundIsConfigured = feetTransform != null && feetTransform != transform
            && feetTransform.IsChildOf(transform) && groundLayer.value != 0
            && groundLayerIndex >= 0 && (groundLayer.value & (1 << groundLayerIndex)) != 0
            && (groundLayer.value & (1 << gameObject.layer)) == 0;
        bool movementSettingsAreValid = IsFinitePositive(maxHorizontalSpeed)
            && IsFinitePositive(acceleration) && IsFinitePositive(deceleration) && IsFinitePositive(turnAcceleration)
            && IsFinitePositive(feetMarkerRadius) && IsFinitePositive(jumpHeight)
            && !float.IsNaN(fallDeathHeight) && !float.IsInfinity(fallDeathHeight);
        bool glideSettingsAreValid = !float.IsNaN(glideTargetFootHeight) && !float.IsInfinity(glideTargetFootHeight)
            && glideTargetFootHeight > fallDeathHeight
            && IsFinitePositive(glideHeightResponse) && IsFinitePositive(glideHeightResponse * glideHeightResponse)
            && IsFinitePositive(2f * glideHeightResponse)
            && IsFinitePositive(glideVerticalAcceleration) && glideVerticalAcceleration <= 10f;
        bool physicsSettingsAreValid = playerRigidbody != null && !playerRigidbody.isKinematic
            && playerRigidbody.useGravity == !IsGliding && playerRigidbody.linearDamping == 0f
            && (playerRigidbody.constraints & RigidbodyConstraints.FreezePositionY) == 0
            && IsFinitePositive(-Physics.gravity.y) && Physics.gravity.x == 0f && Physics.gravity.z == 0f;

        if (playerInput != null && groundIsConfigured && movementSettingsAreValid && glideSettingsAreValid && physicsSettingsAreValid)
            return;
        Debug.LogError("PlayerMovement: check input actions, child ground probe, Ground mask, dynamic gravity Rigidbody (zero damping, Y unlocked), downward gravity, finite positive movement/jump values, finite target foot height above fall death height, positive finite glide response (including squared/doubled values), and glide acceleration in (0, 10].", this);
        enabled = false;
    }

    private static bool IsFinitePositive(float value) => value > 0f && !float.IsInfinity(value);

    // ── 탈락 안내와 디버그 표시 ──

    private void OnGUI() // 화면을 그릴 때 탈락 안내를 표시한다.
    {
        if (HasLocalControl && IsEliminated)
            eliminationOverlay.Draw();
    }

    public void BindLocalControl(TopDownCameraFollow follow, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        localBinding = true;
        cameraFollow = follow;
        startingPosition = spawnPosition;
        startingRotation = spawnRotation;
        playerRigidbody.isKinematic = false;
        playerRigidbody.useGravity = !IsGliding;
        ValidateSettings();
        if (isActiveAndEnabled && !controlSuspended)
            inputReader.Enable(Application.isFocused);
        if (follow != null)
            follow.BindTarget(transform);
    }

    public void UnbindLocalControl()
    {
        inputReader?.Disable();
        ResetRequests();
        groundSupport.Clear();
        requiresLocalBinding = true;
        localBinding = false;
        cameraFollow = null;
        if (playerRigidbody != null)
            playerRigidbody.isKinematic = true;
    }

    public void SetControlSuspended(bool suspended)
    {
        controlSuspended = suspended;
        ResetRequests();
        if (suspended)
        {
            inputReader?.Disable();
            StopHorizontalMovement();
        }
        else if (isActiveAndEnabled && HasLocalControl)
            inputReader?.Enable(Application.isFocused);
    }

    public void ApplyRemoteState(int value)
    {
        if (HasLocalControl || value < 0 || value > (int)PlayerState.Eliminated)
            return;
        state = (PlayerState)value;
        UpdateGliderVisual();
    }

    private void OnDrawGizmosSelected() // Editor에서 선택한 플레이어의 발 위치를 표시한다.
    {
        if (feetTransform == null)
            return;
        Gizmos.color = state == PlayerState.Grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(feetTransform.position, feetMarkerRadius);
    }
}
