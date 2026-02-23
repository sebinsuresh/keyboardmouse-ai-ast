namespace keyboardmouse.input;

/// <summary>
/// Semantic input actions that separate Windows API concerns from sequence detection logic.
/// </summary>
internal enum InputAction
{
    Back,

    // Grid navigation actions (3x3 grid layout)
    GridTopLeft,
    GridTopCenter,
    GridTopRight,
    GridMiddleLeft,
    GridCenter,
    GridMiddleRight,
    GridBottomLeft,
    GridBottomCenter,
    GridBottomRight,
}
