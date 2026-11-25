using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Reuses game objects instead of creating and destroying them during waves (enemies, projectiles,
    /// flashes). Objects are grouped by a key: everything under one key must be built the same way.
    /// Get returns a hidden one (or builds a new one the first time); Release hides it and keeps it for later.
    /// The pool lives in the scene (no statics, rule 6) and is emptied with the scene.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        readonly Dictionary<string, Stack<GameObject>> _free = new();

        [Tooltip("Read only: objects built so far. It should almost stop growing after the first waves.")]
        [SerializeField] int created;
        [Tooltip("Read only: objects waiting to be reused right now.")]
        [SerializeField] int waiting;
        [Tooltip("Read only: how many times a hidden object was used again instead of building a new one.")]
        [SerializeField] int reused;

        public int CreatedCount => created;

        /// <summary>A ready object for this key, active and under `parent`. `create` builds one when none is free.</summary>
        public GameObject Get(string key, Transform parent, Func<GameObject> create)
        {
            GameObject go = null;
            if (_free.TryGetValue(key, out var stack))
            {
                // Skip anything destroyed while it was waiting (scene cleanup, restart).
                while (stack.Count > 0 && go == null) go = stack.Pop();
            }

            if (go == null)
            {
                go = create();
                created++;
            }
            else
            {
                waiting--;
                reused++;
            }

            go.transform.SetParent(parent, false);
            go.SetActive(true);
            return go;
        }

        /// <summary>Hides the object and keeps it for the next Get with the same key.</summary>
        public void Release(string key, GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            if (!_free.TryGetValue(key, out var stack)) _free[key] = stack = new Stack<GameObject>();
            stack.Push(go);
            waiting++;
        }

        /// <summary>Release after `seconds` of game time (short-lived flashes).</summary>
        public void ReleaseAfter(string key, GameObject go, float seconds)
        {
            StartCoroutine(ReleaseLater(key, go, seconds));
        }

        IEnumerator ReleaseLater(string key, GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            // It may have been released or destroyed meanwhile (restart clears the effects).
            if (go != null && go.activeSelf) Release(key, go);
        }
    }
}
