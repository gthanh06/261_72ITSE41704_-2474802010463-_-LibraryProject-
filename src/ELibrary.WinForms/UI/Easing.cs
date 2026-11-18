namespace ELibrary.WinForms.UI;

/// <summary>Easing curves (progress 0..1 → eased 0..1).</summary>
internal static class Easing
{
    /// <summary>Default: fast start, soft landing.</summary>
    public static float OutCubic(float t) => 1f - MathF.Pow(1f - t, 3);

    /// <summary>Stronger deceleration, for things entering the screen.</summary>
    public static float OutQuint(float t) => 1f - MathF.Pow(1f - t, 5);

    /// <summary>Accelerate then decelerate, for things moving across the screen.</summary>
    public static float InOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3) / 2f;

    /// <summary>Slight overshoot, for things popping in.</summary>
    public static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3) + c1 * MathF.Pow(t - 1f, 2);
    }
}
