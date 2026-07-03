using Godot;

public static class BattleViewUtility
{
    public static Rect2 TransformRectToGlobal(Node2D node, Rect2 localRect)
    {
        Vector2 topLeft = node.ToGlobal(localRect.Position);
        Vector2 topRight = node.ToGlobal(localRect.Position + new Vector2(localRect.Size.X, 0f));
        Vector2 bottomLeft = node.ToGlobal(localRect.Position + new Vector2(0f, localRect.Size.Y));
        Vector2 bottomRight = node.ToGlobal(localRect.End);

        float minX = Mathf.Min(Mathf.Min(topLeft.X, topRight.X), Mathf.Min(bottomLeft.X, bottomRight.X));
        float minY = Mathf.Min(Mathf.Min(topLeft.Y, topRight.Y), Mathf.Min(bottomLeft.Y, bottomRight.Y));
        float maxX = Mathf.Max(Mathf.Max(topLeft.X, topRight.X), Mathf.Max(bottomLeft.X, bottomRight.X));
        float maxY = Mathf.Max(Mathf.Max(topLeft.Y, topRight.Y), Mathf.Max(bottomLeft.Y, bottomRight.Y));

        return new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
    }

    public static Vector2 GetBottomCenter(Rect2 localRect)
    {
        return new Vector2(localRect.Position.X + localRect.Size.X * 0.5f, localRect.Position.Y + localRect.Size.Y);
    }

    public static Vector2 GetTopCenter(Rect2 localRect)
    {
        return new Vector2(localRect.Position.X + localRect.Size.X * 0.5f, localRect.Position.Y);
    }

    public static Vector2 ClampOffsetInside(Rect2 bounds, Rect2 safeArea)
    {
        float offsetX = 0f;
        float offsetY = 0f;

        if (bounds.Position.X < safeArea.Position.X)
        {
            offsetX = safeArea.Position.X - bounds.Position.X;
        }
        else if (bounds.End.X > safeArea.End.X)
        {
            offsetX = safeArea.End.X - bounds.End.X;
        }

        if (bounds.Position.Y < safeArea.Position.Y)
        {
            offsetY = safeArea.Position.Y - bounds.Position.Y;
        }
        else if (bounds.End.Y > safeArea.End.Y)
        {
            offsetY = safeArea.End.Y - bounds.End.Y;
        }

        return new Vector2(offsetX, offsetY);
    }
}
