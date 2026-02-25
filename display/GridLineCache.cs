using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using System.Collections.Generic;

namespace keyboardmouse.display;

/// <summary>
/// Cache for rendered grid line bitmaps, keyed by grid dimensions (width, height).
/// Manages GDI bitmap lifetime with configurable eviction policy.
/// </summary>
internal sealed class GridLineCache : IDisposable
{
    private readonly int _maxDepths;
    private readonly Dictionary<(int w, int h), HBITMAP> _cache;
    private readonly Queue<(int w, int h)> _accessOrder;

    /// <summary>
    /// Initialize the cache.
    /// </summary>
    /// <param name="maxDepths">Maximum number of distinct grid sizes to cache.
    /// 0 means unlimited cache size.</param>
    internal GridLineCache(int maxDepths)
    {
        _maxDepths = maxDepths;
        _cache = new();
        _accessOrder = new();
    }

    /// <summary>
    /// Try to retrieve a cached grid line bitmap for the given dimensions.
    /// </summary>
    internal bool TryGet((int w, int h) key, out HBITMAP bitmap)
    {
        return _cache.TryGetValue(key, out bitmap);
    }

    /// <summary>
    /// Store a rendered grid line bitmap in the cache.
    /// If the cache is at capacity and maxDepths > 0, evicts the oldest entry.
    /// </summary>
    internal void Store((int w, int h) key, HBITMAP bitmap)
    {
        // If already cached, do nothing (shouldn't happen in normal flow, but be defensive)
        if (_cache.ContainsKey(key))
        {
            return;
        }

        // If cache is full and has a limit, evict the oldest entry
        if (_maxDepths > 0 && _cache.Count >= _maxDepths)
        {
            if (_accessOrder.TryDequeue(out var oldestKey))
            {
                if (_cache.TryGetValue(oldestKey, out var oldBitmap))
                {
                    PInvoke.DeleteObject(oldBitmap);
                    _cache.Remove(oldestKey);
                }
            }
        }

        _cache[key] = bitmap;
        _accessOrder.Enqueue(key);
    }

    public void Dispose()
    {
        foreach (var bitmap in _cache.Values)
        {
            PInvoke.DeleteObject(bitmap);
        }
        _cache.Clear();
        _accessOrder.Clear();
    }
}
