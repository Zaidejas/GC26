using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Minimal pixel-art look for the traveler (based on MC Concept Art No.1): 6-frame run, jump, fall, slide.
    /// A 1 px "aura" outline around him is tinted with the current world's colour, so the rule
    /// "same colour as the platform = solid for you" stays readable without recolouring the character.
    /// Frames live in Resources/gc26_timetraveler/Traveler/ (built from the artist's sprite by Tools~/traveler_from_art.py).
    /// </summary>
    public class TravelerAnimator : MonoBehaviour
    {
        const string Folder = TimetravelerGame.Id + "/Traveler/" + TimetravelerGame.Id + "_traveler_";
        const int RunFrameCount = 6;

        [Tooltip("Run frames played per unit travelled (6 frames = one full stride)")]
        public float runFramesPerUnit = 1.5f;

        static readonly Color DeadTint = new Color(1f, 0.55f, 0.55f);

        TravelerController _traveler;
        SpriteRenderer _body;
        SpriteRenderer _aura;
        Sprite[] _run, _runAura;
        Sprite _jump, _jumpAura, _fall, _fallAura, _slide, _slideAura;
        float _runPhase;
        float _lastX;
        Vector2 _squash = Vector2.one;   // squash and stretch, springs back to 1

        /// <summary>Squash (x &gt; 1, y &lt; 1) or stretch (x &lt; 1, y &gt; 1) the sprite for a moment.</summary>
        public void Punch(float scaleX, float scaleY) => _squash = new Vector2(scaleX, scaleY);

        /// <summary>Loads the frames; returns null (and the caller keeps the plain box) if any are missing.</summary>
        public static TravelerAnimator TryCreate(TravelerController traveler, Material material)
        {
            var run = new Sprite[RunFrameCount];
            var runAura = new Sprite[RunFrameCount];
            for (var i = 0; i < RunFrameCount; i++)
            {
                run[i] = Load("run" + i);
                runAura[i] = Load("run" + i + "_aura");
                if (run[i] == null || runAura[i] == null) return Missing("run" + i);
            }

            var go = new GameObject("TravelerSprite");
            go.transform.SetParent(traveler.transform, false);
            var anim = go.AddComponent<TravelerAnimator>();
            anim._traveler = traveler;
            anim._run = run;
            anim._runAura = runAura;
            anim._jump = Load("jump"); anim._jumpAura = Load("jump_aura");
            anim._fall = Load("fall"); anim._fallAura = Load("fall_aura");
            anim._slide = Load("slide"); anim._slideAura = Load("slide_aura");
            if (anim._jump == null || anim._fall == null || anim._slide == null)
            {
                Destroy(go);
                return Missing("jump/fall/slide");
            }

            anim._body = go.AddComponent<SpriteRenderer>();
            anim._body.sortingOrder = 3;
            var auraObject = new GameObject("TravelerAura");
            auraObject.transform.SetParent(go.transform, false);
            anim._aura = auraObject.AddComponent<SpriteRenderer>();
            anim._aura.sortingOrder = 2;
            if (material != null)
            {
                anim._body.sharedMaterial = material;
                anim._aura.sharedMaterial = material;
            }
            anim._lastX = traveler.transform.position.x;
            anim.Show(run[0], runAura[0]);
            return anim;
        }

        /// <summary>One frame for a still picture (the scene preview outside Play mode).</summary>
        public static Sprite LoadPreviewFrame() => Load("run0");

        static Sprite Load(string frame) => Resources.Load<Sprite>(Folder + frame);

        static TravelerAnimator Missing(string what)
        {
            TimetravelerLog.Warn($"Traveler sprites not found ({what}) in Resources/{Folder}*, using the plain box.");
            return null;
        }

        public void SetWorldColor(Color color)
        {
            if (_aura != null) _aura.color = color;
        }

        void LateUpdate()
        {
            if (_traveler == null) return;

            // Sprites have their pivot at the feet: keep the feet on the bottom of the collider.
            transform.localPosition = new Vector3(0f, -_traveler.ColliderHeight / 2f, 0f);
            _squash = Vector2.Lerp(_squash, Vector2.one, 1f - Mathf.Exp(-14f * Time.deltaTime));
            transform.localScale = new Vector3(_squash.x, _squash.y, 1f);   // pivot at the feet: they stay put

            var x = _traveler.transform.position.x;
            var moved = Mathf.Max(0f, x - _lastX);
            _lastX = x;

            if (_traveler.IsDead)
            {
                _body.color = DeadTint;
                Show(_fall, _fallAura);
                return;
            }
            _body.color = Color.white;

            if (_traveler.IsSliding) Show(_slide, _slideAura);
            else if (!_traveler.IsGrounded) Show(_traveler.VelocityY > 0f ? _jump : _fall,
                _traveler.VelocityY > 0f ? _jumpAura : _fallAura);
            else
            {
                // Big jumps in x (restart teleport) do not spin the animation.
                if (moved < 2f) _runPhase = Mathf.Repeat(_runPhase + moved * runFramesPerUnit, RunFrameCount);
                var frame = Mathf.Clamp((int)_runPhase, 0, RunFrameCount - 1);
                Show(_run[frame], _runAura[frame]);
            }
        }

        void Show(Sprite body, Sprite aura)
        {
            _body.sprite = body;
            _aura.sprite = aura;
        }
    }
}
