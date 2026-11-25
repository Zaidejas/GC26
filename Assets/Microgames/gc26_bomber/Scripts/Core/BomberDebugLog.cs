using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GC26.Core;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Bug journal: catches errors, exceptions and warnings from our code (Application.logMessageReceived) and writes each
    /// one with a snapshot of the game state into persistentDataPath/gc26_bomber/bug_log.txt. The pause menu shows the
    /// last entries and copies a report to the clipboard. Owned by BomberContext (subscribed while it is enabled).
    /// </summary>
    public sealed class BomberDebugLog
    {
        public sealed class Entry { public DateTime Time; public LogType Type; public string Message, Stack, State; public int Count = 1; }

        const int MaxEntries = 60, MaxFileBytes = 256 * 1024;
        readonly List<Entry> _entries = new List<Entry>();
        readonly Func<string> _state;
        readonly string _path;
        public IReadOnlyList<Entry> Entries => _entries;
        public int Unseen { get; private set; }
        public string FilePath => _path;
        /// <summary>Raised for every new error / exception (not warnings), so the UI can show a short notice.</summary>
        public event Action<Entry> ErrorLogged;

        public BomberDebugLog(Func<string> state)
        {
            _state = state;
            try { _path = Path.Combine(MicrogameSave.Folder(BomberGame.Id), "bug_log.txt"); } catch { _path = null; }
        }

        public void Attach() => Application.logMessageReceived += OnLog;
        public void Detach() => Application.logMessageReceived -= OnLog;
        public void MarkSeen() => Unseen = 0;

        public void Clear()
        {
            _entries.Clear(); Unseen = 0;
            try { if (_path != null && File.Exists(_path)) File.Delete(_path); } catch { }
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            // only our own problems: anything with our namespace / id in it, or any exception and assert
            bool ours = type == LogType.Exception || type == LogType.Assert || message.Contains(BomberGame.Id) || (stack != null && stack.Contains("GC26.Bomber"));
            if (!ours) return;
            var last = _entries.Count > 0 ? _entries[_entries.Count - 1] : null;
            if (last != null && last.Message == message && (DateTime.Now - last.Time).TotalSeconds < 5) { last.Count++; last.Time = DateTime.Now; return; }
            string state; try { state = _state?.Invoke(); } catch (Exception e) { state = "state unavailable: " + e.Message; }
            var en = new Entry { Time = DateTime.Now, Type = type, Message = message, Stack = stack, State = state };
            _entries.Add(en); if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
            if (type != LogType.Warning) { Unseen++; ErrorLogged?.Invoke(en); }
            Write(en);
        }

        void Write(Entry e)
        {
            if (_path == null) return;
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxFileBytes) File.Delete(_path);
                File.AppendAllText(_path, Format(e, true) + "\n");
            }
            catch { /* the journal must never break the game */ }
        }

        static string Format(Entry e, bool stack)
        {
            var sb = new StringBuilder();
            sb.Append($"[{e.Time:yyyy-MM-dd HH:mm:ss}] {e.Type}{(e.Count > 1 ? $" ×{e.Count}" : "")}: {e.Message}\n  state: {e.State}");
            if (stack && !string.IsNullOrEmpty(e.Stack)) sb.Append("\n  " + e.Stack.Trim().Replace("\n", "\n  "));
            return sb.ToString();
        }

        /// <summary>Text to paste into a bug report: device, version and the last entries with stack traces.</summary>
        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"«Последний сигнал» ({BomberGame.Id}) — отчёт об ошибках");
            sb.AppendLine($"Unity {Application.unityVersion} · {Application.platform} · {SystemInfo.deviceModel} · {SystemInfo.operatingSystem} · экран {Screen.width}×{Screen.height} dpi {Screen.dpi:0}");
            sb.AppendLine($"Записей: {_entries.Count}");
            foreach (var e in _entries) sb.AppendLine(Format(e, true));
            return sb.ToString();
        }

        public static string Short(Entry e) => $"{e.Time:HH:mm:ss} {(e.Type == LogType.Warning ? "предупр." : "ошибка")}{(e.Count > 1 ? $" ×{e.Count}" : "")}: {e.Message}";
    }
}
