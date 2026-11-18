using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// The traveler's pixel-art animations, from the artist's sprite sheet:
    /// run (8 frames, by distance), jump (take-off, rise, fall, landing), slide (in, hold, out) and death (plays once).
    /// A 1 px "aura" outline around her is tinted with the current world's colour, so the rule
    /// "same colour as the platform = solid for you" stays readable without recolouring the character.
    /// Frames live in Resources/gc26_timetraveler/Traveler/ (cut from the sheet by Tools~/traveler_from_sheet.py).
    /// </summary>
    public class TravelerAnimator : MonoBehaviour
    {
        const string Folder = TimetravelerGame.Id + "/Traveler/" + TimetravelerGame.Id + "_traveler_";
        const int RunFrames = 8, JumpFrames = 6, SlideFrames = 4, DeathFrames = 6;

        // jump sheet: 0 crouch, 1 push-off, 2 rising, 3 falling, 4 landing crouch, 5 back to running
        const int JumpCrouch = 0, JumpPush = 1, JumpRise = 2, JumpFall = 3, JumpLand = 4, JumpRecover = 5;
        // slide sheet: 0-1 dropping into it, 2 sliding, 3 getting up
        const int SlideHold = 2, SlideOut = 3;

        [Tooltip("Run frames played per unit travelled (8 frames = one full stride)")]
        public float runFramesPerUnit = 0.9f;
        [Tooltip("Seconds per frame of the short one-shot bits (take-off, landing, slide in/out)")]
        public float quickFrame = 0.1f;
        [Tooltip("Seconds per frame of the death animation")]
        public float deathFrame = 0.14f;

        TravelerController _traveler;
        SpriteRenderer _body;
        SpriteRenderer _aura;
        Sprite[] _run, _runAura, _jump, _jumpAura, _slide, _slideAura, _death, _deathAura;
        float _runPhase;
        float _lastX;
        Vector2 _squash = Vector2.one;   // squash and stretch, springs back to 1

        // Boss fight poses: shown for a moment instead of the normal animation.
        public enum Pose { Attack, Dodge, Hurt }
        Sprite[] _poseSet, _poseAuraSet;
        int _poseFrame;
        float _poseTime;

        /// <summary>Faces left (the boss fight: the boss is behind her).</summary>
        public bool FaceLeft
        {
            get => _body != null && _body.flipX;
            set { if (_body != null) { _body.flipX = value; _aura.flipX = value; } }
        }

        /// <summary>Boss fight: strike, duck or get hit, for <paramref name="seconds"/>.</summary>
        public void PlayPose(Pose pose, float seconds = 0.25f)
        {
            switch (pose)
            {
                case Pose.Attack: _poseSet = _jump; _poseAuraSet = _jumpAura; _poseFrame = JumpPush; break;
                case Pose.Dodge: _poseSet = _slide; _poseAuraSet = _slideAura; _poseFrame = SlideHold; break;
                default: _poseSet = _death; _poseAuraSet = _deathAura; _poseFrame = 0; break;
            }
            _poseTime = seconds;
        }

        float _sinceJump = 99f, _sinceLand = 99f, _sinceSlideStart = 99f, _sinceSlideEnd = 99f, _sinceDeath;
        bool _wasSliding, _wasDead;

        /// <summary>Squash (x &gt; 1, y &lt; 1) or stretch (x &lt; 1, y &gt; 1) the sprite for a moment.</summary>
        public void Punch(float scaleX, float scaleY) => _squash = new Vector2(scaleX, scaleY);

        /// <summary>Loads the frames; returns null (and the caller keeps the plain box) if any are missing.</summary>
        public static TravelerAnimator TryCreate(TravelerController traveler, Material material)
        {
            if (!LoadSet("run", RunFrames, out var run, out var runAura)
                || !LoadSet("jump", JumpFrames, out var jump, out var jumpAura)
                || !LoadSet("slide", SlideFrames, out var slide, out var slideAura)
                || !LoadSet("death", DeathFrames, out var death, out var deathAura))
                return null;

            var go = new GameObject("TravelerSprite");
            go.transform.SetParent(traveler.transform, false);
            var anim = go.AddComponent<TravelerAnimator>();
            anim._traveler = traveler;
            anim._run = run; anim._runAura = runAura;
            anim._jump = jump; anim._jumpAura = jumpAura;
            anim._slide = slide; anim._slideAura = slideAura;
            anim._death = death; anim._deathAura = deathAura;

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
            traveler.Jumped += anim.OnJumped;
            traveler.Landed += anim.OnLanded;
            anim._lastX = traveler.transform.position.x;
            anim.Show(run, runAura, 0);
            return anim;
        }

        /// <summary>A set of frames ("run", "jump", "slide", "death") for other screens; null if any is missing.</summary>
        public static Sprite[] LoadFrames(string name, int count) => LoadSet(name, count, out var frames, out _) ? frames : null;

        /// <summary>The run frames for other screens (the map's walking marker); null if missing.</summary>
        public static Sprite[] LoadRunFrames() => LoadSet("run", RunFrames, out var frames, out _) ? frames : null;

        /// <summary>One frame for a still picture (the scene preview outside Play mode, the map marker).</summary>
        public static Sprite LoadPreviewFrame() => Load("run0");

        static Sprite Load(string frame) => Resources.Load<Sprite>(Folder + frame);

        static bool LoadSet(string name, int count, out Sprite[] frames, out Sprite[] auras)
        {
            frames = new Sprite[count];
            auras = new Sprite[count];
            for (var i = 0; i < count; i++)
            {
                frames[i] = Load(name + i);
                auras[i] = Load(name + i + "_aura");
                if (frames[i] == null || auras[i] == null)
                {
                    TimetravelerLog.Warn($"Traveler sprites not found ({name}{i}) in Resources/{Folder}*, using the plain box.");
                    return false;
                }
            }
            return true;
        }

        void OnDestroy()
        {
            if (_traveler == null) return;
            _traveler.Jumped -= OnJumped;
            _traveler.Landed -= OnLanded;
        }

        void OnJumped(bool isDouble) => _sinceJump = isDouble ? quickFrame : 0f;   // a double jump skips the crouch
        void OnLanded(float impact) => _sinceLand = 0f;

        public void SetWorldColor(Color color)
        {
            if (_aura != null) _aura.color = color;
        }

        void LateUpdate()
        {
            if (_traveler == null) return;

            // Sprites have their pivot at the feet: keep the feet on the bottom of the collider.
            transform.localPosition = new Vector3(0f, -_traveler.ColliderHeight / 2f, 0f);
            var dt = Time.deltaTime;
            _squash = Vector2.Lerp(_squash, Vector2.one, 1f - Mathf.Exp(-14f * dt));
            transform.localScale = new Vector3(_squash.x, _squash.y, 1f);   // pivot at the feet: they stay put

            var x = _traveler.transform.position.x;
            var moved = Mathf.Max(0f, x - _lastX);
            _lastX = x;
            _sinceJump += dt;
            _sinceLand += dt;
            _sinceSlideStart += dt;
            _sinceSlideEnd += dt;

            if (_traveler.IsDead)
            {
                if (!_wasDead) _sinceDeath = 0f;
                _wasDead = true;
                _sinceDeath += Time.unscaledDeltaTime;
                Show(_death, _deathAura, Mathf.Min(DeathFrames - 1, (int)(_sinceDeath / deathFrame)));
                return;
            }
            if (_wasDead)   // restarted: start clean
            {
                _wasDead = false;
                _sinceJump = _sinceLand = _sinceSlideStart = _sinceSlideEnd = 99f;
            }

            if (_traveler.IsHalted)
            {
                // Boss fight: a pose for a moment, else the ready stance (crouched take-off frame).
                _poseTime -= dt;
                if (_poseTime > 0f && _poseSet != null) Show(_poseSet, _poseAuraSet, _poseFrame);
                else Show(_jump, _jumpAura, JumpCrouch);
                return;
            }
            _poseTime = 0f;

            var sliding = _traveler.IsSliding;
            if (sliding && !_wasSliding) _sinceSlideStart = 0f;
            if (!sliding && _wasSliding) _sinceSlideEnd = 0f;
            _wasSliding = sliding;

            if (sliding)
            {
                Show(_slide, _slideAura, Mathf.Min(SlideHold, (int)(_sinceSlideStart / quickFrame)));
            }
            else if (!_traveler.IsGrounded)
            {
                int f;
                if (_sinceJump < quickFrame) f = JumpCrouch;
                else if (_sinceJump < quickFrame * 3f) f = JumpPush;
                else f = _traveler.VelocityY > 0f ? JumpRise : JumpFall;
                Show(_jump, _jumpAura, f);
            }
            else if (_sinceLand < quickFrame * 2f)
            {
                Show(_jump, _jumpAura, _sinceLand < quickFrame ? JumpLand : JumpRecover);
            }
            else if (_sinceSlideEnd < quickFrame * 1.5f)
            {
                Show(_slide, _slideAura, SlideOut);
            }
            else
            {
                // Big jumps in x (restart teleport) do not spin the animation.
                if (moved < 2f) _runPhase = Mathf.Repeat(_runPhase + moved * runFramesPerUnit, RunFrames);
                Show(_run, _runAura, Mathf.Clamp((int)_runPhase, 0, RunFrames - 1));
            }
        }

        void Show(Sprite[] frames, Sprite[] auras, int i)
        {
            _body.sprite = frames[i];
            _aura.sprite = auras[i];
        }
    }
}
