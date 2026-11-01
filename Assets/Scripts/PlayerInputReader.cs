using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 키 해제·초점·입력 구독만 관리한다. 접지 여부와 점프 실행은 PlayerMovement가 판단한다.
internal sealed class PlayerInputReader
{
    private readonly PlayerInputActions actions;
    private bool enabled;
    private bool eliminated;
    private bool waitingForMoveKeyRelease;
    private bool jumpReleaseConfirmed;
    private bool jumpHeld;
    private bool restartReleaseConfirmed;

    internal event Action JumpPressed;
    internal event Action JumpReleased;
    internal event Action RestartPressed;
    internal Vector2 Move { get; private set; }
    internal bool HasFocus { get; private set; }
    internal bool CanMove => enabled && HasFocus && !eliminated && !waitingForMoveKeyRelease;
    internal bool IsJumpHeld => enabled && HasFocus && !eliminated && jumpHeld && actions.Player.Jump.IsPressed();

    internal PlayerInputReader(PlayerInputActions actions) => this.actions = actions;

    internal void Enable(bool hasFocus)
    {
        SetFocus(hasFocus);
        if (enabled)
            return;
        enabled = true;
        actions.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
        actions.Player.Move.performed += HandleMove;
        actions.Player.Move.canceled += HandleMove;
        actions.Player.Jump.performed += HandleJump;
        actions.Player.Jump.canceled += HandleJumpReleased;
        actions.Player.Restart.performed += HandleRestart;
        InputSystem.onAfterUpdate += UpdateReleasedKeys;
        actions.Player.Enable();
    }

    internal void Disable()
    {
        enabled = false;
        actions.Player.Move.performed -= HandleMove;
        actions.Player.Move.canceled -= HandleMove;
        actions.Player.Jump.performed -= HandleJump;
        actions.Player.Jump.canceled -= HandleJumpReleased;
        actions.Player.Restart.performed -= HandleRestart;
        InputSystem.onAfterUpdate -= UpdateReleasedKeys;
        actions.Player.Disable();
        ResetKeys();
    }

    internal void SetFocus(bool hasFocus)
    {
        HasFocus = hasFocus;
        ResetKeys();
    }

    internal void SetEliminated(bool value)
    {
        eliminated = value;
        jumpReleaseConfirmed = false;
        jumpHeld = false;
        Move = Vector2.zero;
        if (!value)
        {
            // 재시작 시 이동 키는 즉시 반영하지만, 누른 채인 점프 키는 다시 무장하지 않는다.
            waitingForMoveKeyRelease = false;
            Move = Vector2.ClampMagnitude(actions.Player.Move.ReadValue<Vector2>(), 1f);
        }
    }

    internal void ClearMovement() => Move = Vector2.zero;

    private void ResetKeys()
    {
        Move = Vector2.zero;
        waitingForMoveKeyRelease = true;
        jumpReleaseConfirmed = false;
        jumpHeld = false;
        restartReleaseConfirmed = false;
        JumpReleased?.Invoke(); // 초점·비활성 경계에서도 유지 입력을 남기지 않는다.
    }

    private void HandleMove(InputAction.CallbackContext context)
    {
        Move = context.performed && CanMove
            ? Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f)
            : Vector2.zero;
    }

    private void HandleJump(InputAction.CallbackContext context)
    {
        if (enabled && HasFocus && !eliminated && jumpReleaseConfirmed)
        {
            jumpHeld = true;
            JumpPressed?.Invoke();
        }
    }

    private void HandleJumpReleased(InputAction.CallbackContext context)
    {
        jumpHeld = false;
        JumpReleased?.Invoke();
    }

    private void HandleRestart(InputAction.CallbackContext context)
    {
        if (enabled && HasFocus && eliminated && restartReleaseConfirmed)
        {
            restartReleaseConfirmed = false;
            RestartPressed?.Invoke();
        }
    }

    private void UpdateReleasedKeys()
    {
        if (!HasFocus)
            return;
        if (AreAllKeysReleased(actions.Player.Restart))
            restartReleaseConfirmed = true;
        if (eliminated)
            return;
        if (AreAllKeysReleased(actions.Player.Jump))
            jumpReleaseConfirmed = true;
        if (waitingForMoveKeyRelease && AreAllKeysReleased(actions.Player.Move))
            waitingForMoveKeyRelease = false;
    }

    private static bool AreAllKeysReleased(InputAction action)
    {
        if (!action.enabled)
            return false;
        // 반대 키를 함께 눌러 이동값만 0인 경우는 키 해제로 취급하지 않는다.
        foreach (var control in action.controls)
            if (control.IsActuated())
                return false;
        return true;
    }
}
