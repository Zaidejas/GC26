using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>What the player has to do at a taught obstacle.</summary>
    public enum TutorialMove { None, Jump, DoubleJump, Slide, Switch, Spring, JumpThenSlide, SlideThenJump, SwitchThenJump }

    /// <summary>
    /// The tutorial's moving example ("video"): a small looping scene in the hint panel where the traveler,
    /// with her real sprites, does the move once: jumps the rock, slides under the log, switches world...
    /// Drawn with IMGUI, no video files. One instance per run (holds the loaded frames, nothing static).
    /// </summary>
    public class RunnerTutorial
    {
        const float Loop = 2f;              // seconds per loop of the example
        static readonly Color Hazard = new Color(0.85f, 0.25f, 0.2f);
        static readonly Color Ground = new Color(0.45f, 0.36f, 0.27f);
        static readonly Color Panel = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        readonly Sprite[] _run, _jump, _slide;

        public RunnerTutorial()
        {
            _run = TravelerAnimator.LoadFrames("run", 8);
            _jump = TravelerAnimator.LoadFrames("jump", 6);
            _slide = TravelerAnimator.LoadFrames("slide", 4);
        }

        /// <summary>The move each taught obstacle asks for (by its hint key in RunnerTrack).</summary>
        public static TutorialMove MoveFor(string key)
        {
            switch (key)
            {
                case "rock": case "gap": case "rockrock": case "rock3": case "boulders":
                case "islands": case "shipdeck": return TutorialMove.Jump;
                case "tallwall": return TutorialMove.DoubleJump;
                case "log": case "tunnel": case "ptero": case "flyer": case "blade": case "crusher":
                case "gears": case "press": return TutorialMove.Slide;
                case "rocklog": case "rocklogrock": return TutorialMove.JumpThenSlide;
                case "logrock": return TutorialMove.SlideThenJump;
                case "worldbridge": case "worldwall": case "worldsplit": return TutorialMove.Switch;
                case "worldwallrock": return TutorialMove.SwitchThenJump;
                case "spring": return TutorialMove.Spring;
                default: return TutorialMove.None;
            }
        }

        /// <summary>The gesture (phone) or keys (keyboard) for a move, in big letters under the example.</summary>
        public static string GestureFor(TutorialMove move, bool touch)
        {
            switch (move)
            {
                case TutorialMove.Jump: return touch ? "TAP" : "SPACE";
                case TutorialMove.DoubleJump: return touch ? "TAP  TAP" : "SPACE  SPACE";
                case TutorialMove.Slide: return touch ? "SWIPE DOWN" : "HOLD  S";
                case TutorialMove.Switch: return touch ? "SWIPE SIDEWAYS" : "Q";
                case TutorialMove.Spring: return "JUST RUN ONTO IT";
                case TutorialMove.JumpThenSlide: return touch ? "TAP, then SWIPE DOWN" : "SPACE, then S";
                case TutorialMove.SlideThenJump: return touch ? "SWIPE DOWN, then TAP" : "S, then SPACE";
                case TutorialMove.SwitchThenJump: return touch ? "SWIPE SIDEWAYS, then TAP" : "Q, then SPACE";
                default: return "";
            }
        }

        /// <summary>
        /// Draws one frame of the example into <paramref name="r"/> (call from OnGUI).
        /// Returns true while the example is doing the move (to light up the gesture text in time with it).
        /// </summary>
        public bool Draw(Rect r, TutorialMove move, float realTime)
        {
            Fill(r, Panel);
            var t = Mathf.Repeat(realTime, Loop) / Loop;
            var groundY = r.y + r.height * 0.84f;
            Fill(new Rect(r.x, groundY, r.width, r.yMax - groundY), Ground);

            var h = r.height * 0.5f;              // the traveler's run frame height on screen
            var px = h / 64f;                     // one sprite pixel
            var cx = Mathf.Lerp(r.x - h * 0.3f, r.xMax - h * 0.2f, t);   // her centre x
            var obstacleX = r.x + r.width * 0.55f;
            var second = r.x + r.width * 0.78f;   // second obstacle of the two-step moves
            var acting = false;

            Sprite frame = null;
            var lift = 0f;                        // height above the ground
            var ducking = false;                  // sliding (box drawn low when there are no sprites)
            var tint = Color.white;
            var runFrame = _run != null ? _run[(int)(realTime * 12f) % _run.Length] : null;

            // Helpers: a jump arc / slide between two x positions.
            bool Arc(float from, float to, float height)
            {
                if (cx < from || cx > to) return false;
                var p = Mathf.InverseLerp(from, to, cx);
                lift = Mathf.Sin(p * Mathf.PI) * height;
                frame = _jump != null ? _jump[p < 0.5f ? 2 : 3] : null;
                return true;
            }
            bool Slide(float from, float to)
            {
                if (cx < from || cx > to) return false;
                frame = _slide != null ? _slide[2] : null;
                ducking = true;
                return true;
            }

            switch (move)
            {
                case TutorialMove.Jump:
                    Rock(obstacleX, groundY, h);
                    acting = Arc(obstacleX - h * 0.85f, obstacleX + h * 0.65f, h * 0.75f);
                    break;
                case TutorialMove.DoubleJump:
                    Fill(new Rect(obstacleX - h * 0.12f, groundY - h * 1.05f, h * 0.24f, h * 1.05f), Hazard);
                    acting = Arc(obstacleX - h * 1.1f, obstacleX + h * 0.8f, h * 1.25f);
                    if (acting && cx > obstacleX - h * 0.4f) Text(new Rect(cx - h, groundY - lift - h * 1.5f, h * 2f, h * 0.5f), "x2", r.height * 0.14f);
                    break;
                case TutorialMove.Slide:
                    Log(obstacleX, r.y, groundY, h);
                    acting = Slide(obstacleX - h * 0.9f, obstacleX + h * 0.9f);
                    break;
                case TutorialMove.JumpThenSlide:
                    Rock(obstacleX - h * 0.6f, groundY, h);
                    Log(second, r.y, groundY, h);
                    acting = Arc(obstacleX - h * 1.4f, obstacleX + h * 0.1f, h * 0.7f) || Slide(second - h * 0.9f, second + h * 0.9f);
                    break;
                case TutorialMove.SlideThenJump:
                    Log(obstacleX - h * 0.6f, r.y, groundY, h);
                    Rock(second, groundY, h);
                    acting = Slide(obstacleX - h * 1.5f, obstacleX + h * 0.3f) || Arc(second - h * 0.85f, second + h * 0.65f, h * 0.75f);
                    break;
                case TutorialMove.Spring:
                {
                    var pad = obstacleX - h * 0.9f;
                    Fill(new Rect(pad - h * 0.3f, groundY - h * 0.1f, h * 0.6f, h * 0.1f), new Color(0.3f, 0.85f, 0.45f));
                    Fill(new Rect(obstacleX + h * 0.1f, r.y + r.height * 0.12f, h * 0.24f, groundY - r.y - r.height * 0.12f), Hazard);
                    acting = Arc(pad, obstacleX + h * 1.5f, r.height * 0.62f);
                    break;
                }
                case TutorialMove.Switch:
                case TutorialMove.SwitchThenJump:
                {
                    // A FUTURE (blue) wall: solid while she is PAST (orange); switching makes it a ghost.
                    var switchAt = obstacleX - h * 1.3f;
                    var switched = cx > switchAt;
                    var future = RunnerWorldState.FutureColor;
                    Fill(new Rect(obstacleX - h * 0.15f, r.y + r.height * 0.08f, h * 0.3f, groundY - r.y - r.height * 0.08f),
                        switched ? new Color(future.r, future.g, future.b, 0.2f) : future);
                    tint = switched ? future : RunnerWorldState.PastColor;
                    acting = Mathf.Abs(cx - switchAt) < h * 0.4f;
                    if (move == TutorialMove.SwitchThenJump)
                    {
                        Rock(second, groundY, h);
                        acting |= Arc(second - h * 0.85f, second + h * 0.65f, h * 0.75f);
                    }
                    break;
                }
            }

            // The traveler (with her world colour as a glow, as in the game).
            var sprite = frame ?? runFrame;
            if (sprite != null)
            {
                var size = new Vector2(sprite.rect.width * px, sprite.rect.height * px);
                var rect = new Rect(cx - size.x / 2f, groundY - lift - size.y, size.x, size.y);
                var glow = new Rect(rect.x - px * 2f, rect.y - px * 2f, rect.width + px * 4f, rect.height + px * 4f);
                var old = GUI.color;
                GUI.color = new Color(tint.r, tint.g, tint.b, tint == Color.white ? 0f : 0.35f);
                DrawSprite(glow, sprite);
                GUI.color = Color.white;
                DrawSprite(rect, sprite);
                GUI.color = old;
            }
            else
            {
                // No sprites: the same coloured box as in the run (lower while sliding).
                var bh = ducking ? h * 0.4f : h * 0.85f;
                var bw = ducking ? h * 0.7f : h * 0.45f;
                Fill(new Rect(cx - bw / 2f, groundY - lift - bh, bw, bh), tint == Color.white ? RunnerWorldState.PastColor : tint);
            }
            return acting;
        }

        static void Rock(float x, float groundY, float h) =>
            Fill(new Rect(x - h * 0.17f, groundY - h * 0.34f, h * 0.34f, h * 0.34f), Hazard);

        static void Log(float x, float top, float groundY, float h) =>
            Fill(new Rect(x - h * 0.4f, top, h * 0.8f, groundY - h * 0.58f - top), Hazard);

        static void DrawSprite(Rect r, Sprite s)
        {
            var tex = s.texture;
            var tr = s.textureRect;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        GUIStyle _style;

        void Text(Rect r, string text, float size)
        {
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _style.fontSize = Mathf.RoundToInt(size);
            GUI.Label(r, text, _style);
        }
    }
}
