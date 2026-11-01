using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class PlayerInputReaderTests
{
    private PlayerInputActions actions;
    private PlayerInputReader reader;
    private Keyboard keyboard;
    private InputSettings.UpdateMode previousUpdateMode;
    private InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
    private int jumpPresses;
    private int restartPresses;

    [SetUp]
    public void SetUp()
    {
        previousUpdateMode = InputSystem.settings.updateMode;
        previousBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        keyboard = InputSystem.AddDevice<Keyboard>();
        actions = new PlayerInputActions();
        actions.devices = new InputDevice[] { keyboard };
        reader = new PlayerInputReader(actions);
        jumpPresses = restartPresses = 0;
        reader.JumpPressed += () => jumpPresses++;
        reader.RestartPressed += () => restartPresses++;
        reader.Enable(true);
        Press();
    }

    [TearDown]
    public void TearDown()
    {
        reader?.Disable();
        actions?.Dispose();
        if (keyboard != null && keyboard.added)
            InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.updateMode = previousUpdateMode;
        InputSystem.settings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
    }

    [Test]
    public void FocusReturnRequiresActualKeyReleaseNotOpposingKeys()
    {
        reader.SetFocus(false);
        Press(Key.W, Key.S);
        reader.SetFocus(true);
        InputSystem.Update();
        Assert.That(reader.CanMove, Is.False);
        Press(Key.W);
        Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
        Press();
        Assert.That(reader.CanMove, Is.True);
        Press(Key.W);
        Assert.That(reader.Move, Is.EqualTo(Vector2.up));
    }

    [Test]
    public void ShortPressSurvivesReleaseAndHoldingDoesNotRepeat()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        Assert.That(jumpPresses, Is.EqualTo(1));
        Press(Key.Space);
        Press(Key.Space);
        Assert.That(jumpPresses, Is.EqualTo(2));
    }

    [Test]
    public void ReactivationDoesNotDuplicateSubscriptionsAndDisableStopsInput()
    {
        for (int i = 0; i < 20; i++)
        {
            reader.Disable();
            reader.Enable(true);
        }
        Press();
        Press(Key.Space);
        Assert.That(jumpPresses, Is.EqualTo(1));
        reader.Disable();
        Press();
        Press(Key.Space, Key.W);
        Assert.That(jumpPresses, Is.EqualTo(1));
        Assert.That(reader.CanMove, Is.False);
        Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void RestartResumesHeldMovementButRequiresFreshJumpPress()
    {
        reader.SetEliminated(true);
        Press(Key.D, Key.Space, Key.R);
        Assert.That(restartPresses, Is.EqualTo(1));
        Assert.That(jumpPresses, Is.Zero);
        Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
        reader.SetEliminated(false);
        Assert.That(reader.Move, Is.EqualTo(Vector2.right));
        InputSystem.Update();
        Assert.That(jumpPresses, Is.Zero);
        Press();
        Press(Key.Space);
        Assert.That(jumpPresses, Is.EqualTo(1));
    }

    [Test]
    public void FocusReturnWithHeldButtonsRequiresReleaseAndFreshPress()
    {
        reader.SetEliminated(true);
        reader.SetFocus(false);
        Press(Key.R, Key.Space);
        reader.SetFocus(true);
        Press(Key.R, Key.Space);
        Assert.That(restartPresses, Is.Zero);
        Press();
        Press(Key.R);
        Assert.That(restartPresses, Is.EqualTo(1));
    }

    private void Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }
}
