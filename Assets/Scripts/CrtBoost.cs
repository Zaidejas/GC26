using UnityEngine;

// Усиление CRT-эффекта на заставках: чуть больше рыбьего глаза и сдвига цветов.
// Добавка складывается с настройками материала CRT, сам материал не меняется
public static class CrtBoost
{
    public const float ExtraFisheye = 0.04f;
    public const float ExtraColorShift = 0.25f;

    static readonly int FisheyeId = Shader.PropertyToID("_CrtExtraFisheye");
    static readonly int ColorShiftId = Shader.PropertyToID("_CrtExtraColorShift");

    // amount: 0 - как в игре, 1 - полное усиление
    public static void Set(float amount)
    {
        Shader.SetGlobalFloat(FisheyeId, ExtraFisheye * amount);
        Shader.SetGlobalFloat(ColorShiftId, ExtraColorShift * amount);
    }
}
