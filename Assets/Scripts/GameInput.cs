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

    // Палец на экране или зажатая мышь в редакторе: где находится в пикселях экрана
    public static bool PointerHeld(out Vector2 screenPosition)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            screenPosition = Input.GetTouch(0).position;
            return true;
        }
        screenPosition = Input.mousePosition;
        return Input.GetMouseButton(0);
#elif ENABLE_INPUT_SYSTEM
        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
        screenPosition = Vector2.zero;
        return false;
#else
        screenPosition = Vector2.zero;
        return false;
#endif
    }

    // Клавиши 1, 2, 3 для тестов способностей
    public static bool AbilityPressed(int index)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Alpha1 + index);
#elif ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return false;
        switch (index)
        {
            case 0: return keyboard.digit1Key.wasPressedThisFrame;
            case 1: return keyboard.digit2Key.wasPressedThisFrame;
            case 2: return keyboard.digit3Key.wasPressedThisFrame;
            default: return false;
        }
#else
        return false;
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
