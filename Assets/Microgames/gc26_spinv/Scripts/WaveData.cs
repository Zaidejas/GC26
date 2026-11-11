using UnityEngine;

namespace GC26.Spinv
{
    // Настройки одной волны. Создать: ПКМ в Project → Create → Space Invaders → Wave
    [CreateAssetMenu(fileName = "Wave", menuName = "Space Invaders/Wave")]
    public class WaveData : ScriptableObject
    {
        [Tooltip("Если задан, вместо сетки пришельцев будет битва с этим боссом")]
        public Boss bossPrefab;

        [Min(1)] public int rows = 4;
        [Min(1)] public int columns = 8;

        [Tooltip("Фигура строя: строки сверху вниз, 'x' - пришелец, '.' - пусто. Если задана, rows/columns берутся из неё")]
        public string[] shape;

        public bool HasShape => shape != null && shape.Length > 0;
        public int Rows => HasShape ? shape.Length : rows;
        public int Columns => HasShape ? shape[0].Length : columns;

        // Есть ли пришелец в клетке
        public bool Filled(int row, int col) =>
            !HasShape || (col < shape[row].Length && shape[row][col] != '.');

        [Tooltip("Сколько усиленных пришельцев будет в волне: случайно от Min до Max")]
        [Min(0)] public int eliteCountMin = 0;
        [Min(0)] public int eliteCountMax = 0;
        [Tooltip("Все пришельцы волны усиленные (метка ELITE в аркаде)")]
        public bool allElite = false;

        [Tooltip("Базовая скорость сетки")]
        public float speed = 1f;

        [Tooltip("Примерно раз во сколько секунд стреляют пришельцы")]
        public float missileInterval = 1.4f;

        [Range(0f, 1f)]
        public float coinDropChance = 0.2f;
    }
}
