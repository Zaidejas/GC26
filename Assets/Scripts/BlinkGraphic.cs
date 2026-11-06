using UnityEngine;
using UnityEngine.UI;

// Мигание надписи «как на автомате»: включено полпериода, выключено полпериода.
// Реальное время - мигает и при Time.timeScale = 0
[RequireComponent(typeof(Graphic))]
public class BlinkGraphic : MonoBehaviour
{
    public float period = 1f;

    Graphic graphic;

    void Awake()
    {
        graphic = GetComponent<Graphic>();
    }

    void OnDisable()
    {
        if (graphic != null) graphic.enabled = true;
    }

    void Update()
    {
        graphic.enabled = Mathf.Repeat(Time.unscaledTime, period) < period * 0.5f;
    }
}
