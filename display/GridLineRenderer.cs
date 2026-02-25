using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Foundation;

namespace keyboardmouse.display;

/// <summary>
/// Renders a 3×3 grid with dashed or dotted lines.
/// Responsible for pen selection based on cell size and drawing the grid pattern.
/// </summary>
internal static class GridLineRenderer
{
    // Cyan color in BGR format (RGB 0,255,255 → 0xFFFF00)
    private static readonly COLORREF GridColor = new(0xFFFF00);

    // Threshold: switch to dotted lines when cells become smaller than this
    private const int DottedThreshold = 30;

    /// <summary>
    /// Determines the appropriate pen style based on cell height.
    /// Dashed for larger cells, dotted for smaller cells.
    /// </summary>
    internal static PEN_STYLE GetPenStyle(int cellHeight)
    {
        return cellHeight >= DottedThreshold ? PEN_STYLE.PS_DASH : PEN_STYLE.PS_DOT;
    }

    /// <summary>
    /// Draws the complete 3×3 grid pattern (outer border + inner dividers) into the provided device context.
    /// Grid is drawn at origin (0, 0) with dimensions determined by cellWidth and cellHeight.
    /// </summary>
    internal static void DrawGridLines(HDC hdc, int cellWidth, int cellHeight)
    {
        // Transparent background mode ensures gaps in dashed/dotted lines show the bitmap background color.
        PInvoke.SetBkMode(hdc, BACKGROUND_MODE.TRANSPARENT);

        PEN_STYLE penStyle = GetPenStyle(cellHeight);
        HPEN gridPen = PInvoke.CreatePen(penStyle, 1, GridColor);
        HGDIOBJ oldPen = PInvoke.SelectObject(hdc, gridPen);

        try
        {
            int gridWidth = cellWidth * 3;
            int gridHeight = cellHeight * 3;

            DrawOuterBorder(hdc, 0, 0, gridWidth, gridHeight);
            DrawInnerGridLines(hdc, 0, 0, gridWidth, gridHeight, cellWidth, cellHeight);
        }
        finally
        {
            PInvoke.SelectObject(hdc, oldPen);
            PInvoke.DeleteObject(gridPen);
        }
    }

    private static void DrawLine(HDC hdc, int x1, int y1, int x2, int y2)
    {
        unsafe
        {
            PInvoke.MoveToEx(hdc, x1, y1, null);
        }
        PInvoke.LineTo(hdc, x2, y2);
    }

    private static void DrawOuterBorder(HDC hdc, int left, int top, int right, int bottom)
    {
        DrawLine(hdc, left, top, right, top);
        DrawLine(hdc, right, top, right, bottom);
        DrawLine(hdc, right, bottom, left, bottom);
        DrawLine(hdc, left, bottom, left, top);
    }

    private static void DrawInnerGridLines(
        HDC hdc,
        int left,
        int top,
        int right,
        int bottom,
        int cellWidth,
        int cellHeight)
    {
        // Two vertical dividers at 1/3 and 2/3 of the width.
        DrawLine(hdc, left + cellWidth, top, left + cellWidth, bottom);
        DrawLine(hdc, left + 2 * cellWidth, top, left + 2 * cellWidth, bottom);

        // Two horizontal dividers at 1/3 and 2/3 of the height.
        DrawLine(hdc, left, top + cellHeight, right, top + cellHeight);
        DrawLine(hdc, left, top + 2 * cellHeight, right, top + 2 * cellHeight);
    }
}
