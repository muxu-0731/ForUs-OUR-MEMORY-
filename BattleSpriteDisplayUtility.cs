using Godot;
using System;
using System.Collections.Generic;

public static class BattleSpriteDisplayUtility
{
    private const float AlphaThreshold = 0.01f;
    private static readonly Dictionary<string, VisibleContentMetrics> MetricsCache = new();

    public readonly struct VisibleContentMetrics
    {
        public VisibleContentMetrics(Rect2I pixelBounds, Vector2I textureSize)
        {
            PixelBounds = pixelBounds;
            TextureSize = textureSize;
        }

        public Rect2I PixelBounds { get; }
        public Vector2I TextureSize { get; }
        public int VisibleWidth => PixelBounds.Size.X;
        public int VisibleHeight => PixelBounds.Size.Y;
        public int BottomInset => TextureSize.Y - (PixelBounds.Position.Y + PixelBounds.Size.Y);
        public bool HasVisiblePixels => VisibleWidth > 0 && VisibleHeight > 0;
    }

    public static bool TryGetVisibleContentMetrics(Texture2D texture, out VisibleContentMetrics metrics)
    {
        metrics = default;
        if (texture == null)
        {
            return false;
        }

        string cacheKey = GetCacheKey(texture);
        if (!string.IsNullOrEmpty(cacheKey) && MetricsCache.TryGetValue(cacheKey, out metrics))
        {
            return metrics.HasVisiblePixels;
        }

        if (!TryScanVisibleContent(texture, out metrics))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(cacheKey))
        {
            MetricsCache[cacheKey] = metrics;
        }

        return metrics.HasVisiblePixels;
    }

    public static float CalculateUniformScale(int visibleHeight, float targetVisualHeight, float minScale, float maxScale)
    {
        if (visibleHeight <= 0)
        {
            return Mathf.Clamp(1f, minScale, maxScale);
        }

        return Mathf.Clamp(targetVisualHeight / visibleHeight, minScale, maxScale);
    }

    private static bool TryScanVisibleContent(Texture2D texture, out VisibleContentMetrics metrics)
    {
        metrics = default;

        Image image;
        try
        {
            image = texture.GetImage();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"BattleSpriteDisplayUtility: failed to read image data from {texture.ResourcePath}: {ex.Message}");
            return false;
        }

        if (image == null || image.IsEmpty())
        {
            return false;
        }

        int width = image.GetWidth();
        int height = image.GetHeight();
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (image.GetPixel(x, y).A <= AlphaThreshold)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return false;
        }

        metrics = new VisibleContentMetrics(
            new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1),
            new Vector2I(width, height));
        return true;
    }

    private static string GetCacheKey(Texture2D texture)
    {
        if (!string.IsNullOrWhiteSpace(texture.ResourcePath))
        {
            return texture.ResourcePath;
        }

        return texture.GetInstanceId().ToString();
    }
}
