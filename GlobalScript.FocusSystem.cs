using System;

public partial class GlobalScript
{
    public const int FocusMin = 0;
    public const int FocusMax = 5;
    public const int FocusStart = 3;

    private int _focusPoints = FocusStart;

    public int FocusPoints => _focusPoints;

    public event Action<int> FocusChanged;

    public void ResetFocus()
    {
        SetFocus(FocusStart);
    }

    public void GainFocus(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetFocus(_focusPoints + amount);
    }

    public void SpendFocus(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetFocus(_focusPoints - amount);
    }

    public bool HasEnoughFocus(int requiredAmount)
    {
        if (requiredAmount <= 0)
        {
            return true;
        }

        return _focusPoints >= requiredAmount;
    }

    public bool TrySpendFocus(int amount)
    {
        if (!HasEnoughFocus(amount))
        {
            return false;
        }

        SpendFocus(amount);
        return true;
    }

    private void SetFocus(int value)
    {
        int clampedValue = Math.Clamp(value, FocusMin, FocusMax);
        if (clampedValue == _focusPoints)
        {
            return;
        }

        _focusPoints = clampedValue;
        FocusChanged?.Invoke(_focusPoints);
    }
}
