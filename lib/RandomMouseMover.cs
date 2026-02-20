using System;

namespace keyboardmouse;

/// <summary>
/// Chooses a random point on one of the monitors and moves the mouse there.
/// </summary>
internal sealed class RandomMouseMover
{
    private readonly Random _rand;

    public RandomMouseMover(Random? rand = null)
    {
        _rand = rand ?? new Random();
    }

    public void MoveRandom()
    {
        var rects = DisplayInfo.GetMonitorRects();
        var r = rects[_rand.Next(rects.Length)];
        int x = r.left + _rand.Next(Math.Max(1, r.right - r.left));
        int y = r.top + _rand.Next(Math.Max(1, r.bottom - r.top));
        MouseInput.MoveTo(x, y);
    }
}
