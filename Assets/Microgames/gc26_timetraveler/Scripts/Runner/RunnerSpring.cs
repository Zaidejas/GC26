using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// A spring pad (trigger): touching it throws the traveler straight up to <see cref="height"/> units,
    /// high over a wall that no jump could clear. The pad squashes for a moment when used.
    /// </summary>
    public class RunnerSpring : MonoBehaviour
    {
        [Tooltip("How high the spring throws the traveler (units)")]
        public float height = 20f;

        Vector3 _restScale;
        float _squash;

        void Awake() => _restScale = transform.localScale;

        /// <summary>Called by the traveler when it lands on the pad.</summary>
        public void Used() => _squash = 1f;

        void Update()
        {
            if (_squash <= 0f) return;
            _squash = Mathf.Max(0f, _squash - Time.deltaTime * 5f);
            transform.localScale = new Vector3(_restScale.x * (1f + 0.25f * _squash), _restScale.y * (1f - 0.5f * _squash), 1f);
        }
    }
}
