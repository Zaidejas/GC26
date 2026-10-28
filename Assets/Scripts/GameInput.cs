using UnityEngine;

// Обёртка над вводом: работает и со старым Input Manager, и с новым Input System
public static class GameInput
{
    // -1 = влево, 1 = вправо, 0 = стоим (A/D и стрелки)
    public static float Horizontal()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetAxisRaw("Horizontal");
#elif ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return 0f;
        float value = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value += 1f;
        return value;
#else
        return 0f;
#endif
    }

    // Esc или P
    public static bool PausePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#elif ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame);
#else
        return false;
#endif
    }
}
