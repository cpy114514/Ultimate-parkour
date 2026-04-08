using System.Collections.Generic;
using UnityEngine;

public static class GameInput
{
    public enum BindingId
    {
        KeyboardWasd,
        KeyboardIjkl,
        KeyboardArrows,
        Gamepad1,
        Gamepad2,
        Gamepad3,
        Gamepad4
    }

    struct BindingConfig
    {
        public KeyCode left;
        public KeyCode right;
        public KeyCode up;
        public KeyCode down;
        public KeyCode confirm;
        public KeyCode alternateConfirm;
        public KeyCode rotate;
        public string horizontalAxis;
        public string verticalAxis;
        public string dpadHorizontalAxis;
        public string dpadVerticalAxis;
        public bool usesAxes;
    }

    static readonly BindingId[] joinBindings =
    {
        BindingId.KeyboardWasd,
        BindingId.KeyboardIjkl,
        BindingId.KeyboardArrows,
        BindingId.Gamepad1,
        BindingId.Gamepad2,
        BindingId.Gamepad3,
        BindingId.Gamepad4
    };

    static readonly Dictionary<BindingId, int> previousMenuHorizontal =
        new Dictionary<BindingId, int>();

    static readonly Dictionary<BindingId, int> previousMenuVertical =
        new Dictionary<BindingId, int>();

    public static IReadOnlyList<BindingId> JoinBindings
    {
        get { return joinBindings; }
    }

    public static float GetHorizontal(BindingId binding)
    {
        BindingConfig config = GetBindings(binding);

        if (config.usesAxes)
        {
            return GetCombinedHorizontalAxis(config);
        }

        float horizontal = 0f;

        if (Input.GetKey(config.left))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(config.right))
        {
            horizontal += 1f;
        }

        return Mathf.Clamp(horizontal, -1f, 1f);
    }

    public static float GetVertical(BindingId binding)
    {
        BindingConfig config = GetBindings(binding);

        if (config.usesAxes)
        {
            return GetCombinedVerticalAxis(config);
        }

        float vertical = 0f;

        if (Input.GetKey(config.down))
        {
            vertical -= 1f;
        }

        if (Input.GetKey(config.up))
        {
            vertical += 1f;
        }

        return Mathf.Clamp(vertical, -1f, 1f);
    }

    public static bool GetJumpHeld(BindingId binding)
    {
        BindingConfig config = GetBindings(binding);
        return Input.GetKey(config.up) || IsConfirmHeld(config);
    }

    public static bool GetJumpPressed(BindingId binding)
    {
        BindingConfig config = GetBindings(binding);
        return Input.GetKeyDown(config.up) || IsConfirmPressed(config);
    }

    public static Vector2Int GetSelectionMove(BindingId binding)
    {
        BindingConfig config = GetBindings(binding);

        if (!config.usesAxes)
        {
            if (Input.GetKeyDown(config.left))
            {
                return Vector2Int.left;
            }

            if (Input.GetKeyDown(config.right))
            {
                return Vector2Int.right;
            }

            if (Input.GetKeyDown(config.up))
            {
                return Vector2Int.up;
            }

            if (Input.GetKeyDown(config.down))
            {
                return Vector2Int.down;
            }

            return Vector2Int.zero;
        }

        int horizontal = GetAxisMenuDirection(
            binding,
            GetCombinedHorizontalAxis(config),
            previousMenuHorizontal
        );

        if (horizontal != 0)
        {
            return new Vector2Int(horizontal, 0);
        }

        int vertical = GetAxisMenuDirection(
            binding,
            GetCombinedVerticalAxis(config),
            previousMenuVertical
        );

        if (vertical != 0)
        {
            return new Vector2Int(0, vertical);
        }

        return Vector2Int.zero;
    }

    public static Vector2Int GetPlacementMove(
        BindingId binding,
        ref float nextHorizontalRepeatTime,
        ref float nextVerticalRepeatTime,
        float now,
        float inputRepeatDelay,
        float inputRepeatRate
    )
    {
        BindingConfig config = GetBindings(binding);
        Vector2Int move = Vector2Int.zero;

        move += GetRepeatedAxisMove(
            binding,
            config,
            ref nextHorizontalRepeatTime,
            now,
            inputRepeatDelay,
            inputRepeatRate,
            true
        );
        move += GetRepeatedAxisMove(
            binding,
            config,
            ref nextVerticalRepeatTime,
            now,
            inputRepeatDelay,
            inputRepeatRate,
            false
        );

        return move;
    }

    public static bool GetConfirmPressed(BindingId binding)
    {
        return IsConfirmPressed(GetBindings(binding));
    }

    public static bool GetLobbyJoinPressed(BindingId binding)
    {
        return GetConfirmPressed(binding);
    }

    public static bool GetRotatePressed(BindingId binding)
    {
        BindingConfig config = GetBindings(binding);
        return config.rotate != KeyCode.None && Input.GetKeyDown(config.rotate);
    }

    public static bool GetRotateHeld(BindingId binding)
    {
        BindingConfig config = GetBindings(binding);
        return config.rotate != KeyCode.None && Input.GetKey(config.rotate);
    }

    public static bool GetPausePressed(BindingId binding)
    {
        if (IsKeyboardBinding(binding))
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }

        int joystickIndex = GetJoystickIndex(binding);
        if (joystickIndex <= 0)
        {
            return false;
        }

        return Input.GetKeyDown(GetJoystickButton(joystickIndex, 7)) ||
               Input.GetKeyDown(GetJoystickButton(joystickIndex, 9));
    }

    public static bool TryGetGamepadPausePressed(out BindingId binding)
    {
        BindingId[] gamepadBindings =
        {
            BindingId.Gamepad1,
            BindingId.Gamepad2,
            BindingId.Gamepad3,
            BindingId.Gamepad4
        };

        for (int i = 0; i < gamepadBindings.Length; i++)
        {
            if (GetPausePressed(gamepadBindings[i]))
            {
                binding = gamepadBindings[i];
                return true;
            }
        }

        binding = BindingId.KeyboardWasd;
        return false;
    }

    public static bool IsKeyboardBinding(BindingId binding)
    {
        return binding == BindingId.KeyboardWasd ||
               binding == BindingId.KeyboardIjkl ||
               binding == BindingId.KeyboardArrows;
    }

    public static string GetBindingDisplayName(BindingId binding)
    {
        switch (binding)
        {
            case BindingId.KeyboardWasd:
                return "WASD";
            case BindingId.KeyboardIjkl:
                return "IJKL";
            case BindingId.KeyboardArrows:
                return "Arrows";
            case BindingId.Gamepad1:
                return "Pad 1";
            case BindingId.Gamepad2:
                return "Pad 2";
            case BindingId.Gamepad3:
                return "Pad 3";
            case BindingId.Gamepad4:
                return "Pad 4";
        }

        return binding.ToString();
    }

    public static void ResetState()
    {
        previousMenuHorizontal.Clear();
        previousMenuVertical.Clear();
        Input.ResetInputAxes();
    }

    static Vector2Int GetRepeatedAxisMove(
        BindingId binding,
        BindingConfig config,
        ref float nextRepeatTime,
        float now,
        float inputRepeatDelay,
        float inputRepeatRate,
        bool horizontal
    )
    {
        int direction = 0;

        if (config.usesAxes)
        {
            float rawValue = horizontal
                ? GetCombinedHorizontalAxis(config)
                : GetCombinedVerticalAxis(config);

            int digital = GetAxisDigitalDirection(rawValue);
            Dictionary<BindingId, int> state = horizontal
                ? previousMenuHorizontal
                : previousMenuVertical;
            int previous = GetPreviousState(state, binding);

            if (digital != 0 && previous == 0)
            {
                direction = digital;
                nextRepeatTime = now + inputRepeatDelay;
            }
            else if (digital != 0 && now >= nextRepeatTime)
            {
                direction = digital;
                nextRepeatTime = now + inputRepeatRate;
            }

            state[binding] = digital;
        }
        else
        {
            KeyCode negative = horizontal ? config.left : config.down;
            KeyCode positive = horizontal ? config.right : config.up;

            if (Input.GetKeyDown(negative))
            {
                direction = -1;
                nextRepeatTime = now + inputRepeatDelay;
            }
            else if (Input.GetKeyDown(positive))
            {
                direction = 1;
                nextRepeatTime = now + inputRepeatDelay;
            }
            else if (Input.GetKey(negative) && now >= nextRepeatTime)
            {
                direction = -1;
                nextRepeatTime = now + inputRepeatRate;
            }
            else if (Input.GetKey(positive) && now >= nextRepeatTime)
            {
                direction = 1;
                nextRepeatTime = now + inputRepeatRate;
            }
        }

        if (direction == 0)
        {
            return Vector2Int.zero;
        }

        return horizontal ? new Vector2Int(direction, 0) : new Vector2Int(0, direction);
    }

    static bool IsConfirmHeld(BindingConfig config)
    {
        return config.alternateConfirm != KeyCode.None && Input.GetKey(config.alternateConfirm) ||
               config.confirm != KeyCode.None && Input.GetKey(config.confirm);
    }

    static bool IsConfirmPressed(BindingConfig config)
    {
        return config.confirm != KeyCode.None && Input.GetKeyDown(config.confirm) ||
               config.alternateConfirm != KeyCode.None && Input.GetKeyDown(config.alternateConfirm);
    }

    static int GetAxisMenuDirection(
        BindingId binding,
        float rawValue,
        Dictionary<BindingId, int> stateCache
    )
    {
        int digital = GetAxisDigitalDirection(rawValue);
        int previous = GetPreviousState(stateCache, binding);
        stateCache[binding] = digital;

        if (digital != 0 && previous == 0)
        {
            return digital;
        }

        return 0;
    }

    static int GetPreviousState(Dictionary<BindingId, int> cache, BindingId binding)
    {
        if (!cache.TryGetValue(binding, out int previous))
        {
            return 0;
        }

        return previous;
    }

    static int GetAxisDigitalDirection(float rawValue)
    {
        if (rawValue <= -0.45f)
        {
            return -1;
        }

        if (rawValue >= 0.45f)
        {
            return 1;
        }

        return 0;
    }

    static float ApplyAxisDeadZone(float rawValue)
    {
        return Mathf.Abs(rawValue) < 0.2f ? 0f : Mathf.Clamp(rawValue, -1f, 1f);
    }

    static BindingConfig GetBindings(BindingId binding)
    {
        switch (binding)
        {
            case BindingId.KeyboardWasd:
                return new BindingConfig
                {
                    left = KeyCode.A,
                    right = KeyCode.D,
                    up = KeyCode.W,
                    down = KeyCode.S,
                    confirm = KeyCode.E,
                    alternateConfirm = KeyCode.Space,
                    rotate = KeyCode.Q
                };

            case BindingId.KeyboardIjkl:
                return new BindingConfig
                {
                    left = KeyCode.J,
                    right = KeyCode.L,
                    up = KeyCode.I,
                    down = KeyCode.K,
                    confirm = KeyCode.U,
                    alternateConfirm = KeyCode.None,
                    rotate = KeyCode.O
                };

            case BindingId.KeyboardArrows:
                return new BindingConfig
                {
                    left = KeyCode.LeftArrow,
                    right = KeyCode.RightArrow,
                    up = KeyCode.UpArrow,
                    down = KeyCode.DownArrow,
                    confirm = KeyCode.Return,
                    alternateConfirm = KeyCode.KeypadEnter,
                    rotate = KeyCode.RightShift
                };

            case BindingId.Gamepad1:
                return CreateGamepadBinding(1);
            case BindingId.Gamepad2:
                return CreateGamepadBinding(2);
            case BindingId.Gamepad3:
                return CreateGamepadBinding(3);
            case BindingId.Gamepad4:
                return CreateGamepadBinding(4);
        }

        return default;
    }

    static BindingConfig CreateGamepadBinding(int joystickIndex)
    {
        return new BindingConfig
        {
            confirm = GetJoystickButton(joystickIndex, 0),
            alternateConfirm = KeyCode.None,
            rotate = GetJoystickButton(joystickIndex, 1),
            horizontalAxis = "Joy" + joystickIndex + "Horizontal",
            verticalAxis = "Joy" + joystickIndex + "Vertical",
            dpadHorizontalAxis = "Joy" + joystickIndex + "DpadHorizontal",
            dpadVerticalAxis = "Joy" + joystickIndex + "DpadVertical",
            usesAxes = true
        };
    }

    static int GetJoystickIndex(BindingId binding)
    {
        switch (binding)
        {
            case BindingId.Gamepad1:
                return 1;
            case BindingId.Gamepad2:
                return 2;
            case BindingId.Gamepad3:
                return 3;
            case BindingId.Gamepad4:
                return 4;
        }

        return -1;
    }

    static KeyCode GetJoystickButton(int joystickIndex, int buttonIndex)
    {
        return (KeyCode)System.Enum.Parse(
            typeof(KeyCode),
            "Joystick" + joystickIndex + "Button" + buttonIndex
        );
    }

    static float GetCombinedHorizontalAxis(BindingConfig config)
    {
        float stick = ApplyAxisDeadZone(Input.GetAxisRaw(config.horizontalAxis));
        float dpad = ApplyAxisDeadZone(Input.GetAxisRaw(config.dpadHorizontalAxis));
        return Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
    }

    static float GetCombinedVerticalAxis(BindingConfig config)
    {
        float stick = ApplyAxisDeadZone(Input.GetAxisRaw(config.verticalAxis));
        float dpad = -ApplyAxisDeadZone(Input.GetAxisRaw(config.dpadVerticalAxis));
        return Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
    }
}
