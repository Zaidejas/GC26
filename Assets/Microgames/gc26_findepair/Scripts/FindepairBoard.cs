using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Findepair
{
    /// <summary>
    /// The rules: shuffle and lay out the cards, check pairs, count moves and time, pause,
    /// make found pairs vanish in a sparkle burst, and report the win.
    /// Time.timeScale is never touched (GC26 rule); everything runs on its own pausable clock.
    /// </summary>
    public class FindepairBoard : MonoBehaviour
    {
        public const float FlipSeconds = 0.25f;
        public const float MismatchSeconds = 0.8f;
        public const float MatchVanishDelaySeconds = 1f;
        public const float VanishSeconds = 0.25f;
        public const float WinDelaySeconds = 0.5f;

        public event Action CardFlipped;
        public event Action<int> MovesChanged;
        public event Action PairMatched;
        public event Action PairMissed;
        public event Action PairVanished;
        public event Action<FindepairResult> Won;
        public event Action<bool> PausedChanged;

        public int Moves { get; private set; }
        public int Matches { get; private set; }
        public float Elapsed { get; private set; }
        public bool Running { get; private set; }
        public bool Paused { get; private set; }
        public bool Locked { get; private set; }
        public IReadOnlyList<Card> Cards => _cards;
        public int PairCount => _art != null ? _art.cards.Length : 0;

        /// <summary>Game-time step: zero while paused.</summary>
        public float Delta => Paused ? 0f : Time.unscaledDeltaTime;

        readonly List<Card> _cards = new List<Card>();
        readonly System.Random _rng = new System.Random();
        RectTransform _grid;
        SparkleBurst _sparkles;
        FindepairArt _art;
        Card _first, _second;

        public void Setup(RectTransform grid, SparkleBurst sparkles, FindepairArt art)
        {
            _grid = grid;
            _sparkles = sparkles;
            _art = art;
        }

        public void NewGame()
        {
            Stop();
            foreach (Transform child in _grid)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            _cards.Clear();
            foreach (var data in FindepairRules.BuildDeck(_art.cards, _rng))
                _cards.Add(Card.Create(_grid, data, _art, () => Delta, OnCardClicked));
            var fitter = _grid.GetComponent<BoardFitter>();
            if (fitter != null) fitter.cardCount = _cards.Count;

            Moves = Matches = 0;
            Elapsed = 0;
            _first = _second = null;
            Locked = false;
            Running = true;
            MovesChanged?.Invoke(0);
        }

        /// <summary>Stops the round (restart, menu, exit): cancels pending checks, vanishes and bursts.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            foreach (var c in _cards) if (c != null) c.StopAllCoroutines();
            if (_sparkles != null) _sparkles.Clear();
            Running = false;
            if (Paused) { Paused = false; PausedChanged?.Invoke(false); }
        }

        public void SetPaused(bool paused)
        {
            if (!Running || Paused == paused) return;
            Paused = paused;
            PausedChanged?.Invoke(paused);
        }

        void Update()
        {
            if (Running && !Paused) Elapsed += Time.unscaledDeltaTime;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SetPaused(true);
        }

        public void OnCardClicked(Card card)
        {
            if (Locked || !Running || Paused || card.IsUp || card.IsMatched) return;
            CardFlipped?.Invoke();
            card.StartCoroutine(card.Flip(true));
            if (_first == null) { _first = card; return; }

            _second = card;
            Moves++;
            MovesChanged?.Invoke(Moves);
            Locked = true; // set before the check starts, so fast taps can't open a third card
            StartCoroutine(CheckPair(_first, _second));
        }

        IEnumerator CheckPair(Card a, Card b)
        {
            yield return Wait(FlipSeconds);
            if (a.Data.id == b.Data.id)
            {
                a.PlayMatch();
                b.PlayMatch();
                Matches++;
                _first = _second = null;
                bool last = Matches == PairCount;
                if (last) Running = false;   // the clock stops on the last match
                else Locked = false;         // other cards can be turned while the pair waits to burst
                PairMatched?.Invoke();
                StartCoroutine(VanishPair(a, b, last));
                yield break;
            }

            PairMissed?.Invoke();
            a.PlayMiss();
            b.PlayMiss();
            yield return Wait(MismatchSeconds);
            a.StartCoroutine(a.Flip(false));
            b.StartCoroutine(b.Flip(false));
            yield return Wait(FlipSeconds);
            _first = _second = null;
            Locked = false;
        }

        IEnumerator VanishPair(Card a, Card b, bool last)
        {
            yield return Wait(MatchVanishDelaySeconds);
            foreach (var c in new[] { a, b })
            {
                _sparkles.Play(c.Rect.TransformPoint(c.Rect.rect.center), c.Rect.rect.width);
                c.StartCoroutine(c.PlayVanish());
            }
            yield return Wait(VanishSeconds);
            PairVanished?.Invoke();
            if (!last) yield break;
            yield return Wait(WinDelaySeconds);
            Won?.Invoke(new FindepairResult(Moves, Mathf.RoundToInt(Elapsed * 1000f)));
        }

        /// <summary>Waits the given game time; stands still while paused.</summary>
        IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Delta) yield return null;
        }
    }
}
