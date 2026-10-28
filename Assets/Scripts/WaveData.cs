using UnityEngine;

// Настройки одной волны. Создать: ПКМ в Project → Create → Space Invaders → Wave
[CreateAssetMenu(fileName = "Wave", menuName = "Space Invaders/Wave")]
public class WaveData : ScriptableObject
{
    [Min(1)] public int rows = 4;
    [Min(1)] public int columns = 8;

    [Tooltip("Базовая скорость сетки")]
    public float speed = 1f;

    [Tooltip("Примерно раз во сколько секунд стреляют пришельцы")]
    public float missileInterval = 1.4f;

    [Range(0f, 1f)]
    public float coinDropChance = 0.2f;
}
