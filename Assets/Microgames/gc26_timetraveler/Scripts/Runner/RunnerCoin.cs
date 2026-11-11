using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>A coin on the track (trigger). The traveler reports it; the session counts and removes it.</summary>
    public class RunnerCoin : MonoBehaviour
    {
        const float BobHeight = 0.15f;
        const float BobSpeed = 4f;

        float _baseY;
        float _offset;

        void Start()
        {
            _baseY = transform.position.y;
            _offset = transform.position.x * 0.7f; // neighbouring coins bob out of step, like a wave
        }

        void Update()
        {
            var p = transform.position;
            p.y = _baseY + Mathf.Sin(Time.time * BobSpeed + _offset) * BobHeight;
            transform.position = p;
        }
    }
}
