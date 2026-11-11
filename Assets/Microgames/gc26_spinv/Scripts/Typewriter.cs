using System.Collections;
using TMPro;
using UnityEngine;

namespace GC26.Spinv
{
    // Печать надписи по буквам «как на терминале» на реальном времени (работает при Time.timeScale = 0).
    // Обычный текст печатается с курсором «_», текст с тегами (цвет) - через maxVisibleCharacters,
    // чтобы теги не ломались на середине
    public static class Typewriter
    {
        public static IEnumerator Type(TMP_Text text, string value, float charTime)
        {
            if (value.IndexOf('<') >= 0)
            {
                text.text = value;
                text.ForceMeshUpdate();
                int count = text.textInfo.characterCount;
                for (int k = 0; k <= count; k++)
                {
                    text.maxVisibleCharacters = k;
                    yield return new WaitForSecondsRealtime(charTime);
                }
                text.maxVisibleCharacters = 99999;
                yield break;
            }

            text.maxVisibleCharacters = 99999;
            for (int k = 1; k <= value.Length; k++)
            {
                text.text = value.Substring(0, k) + "_";
                yield return new WaitForSecondsRealtime(charTime);
            }
            text.text = value;
        }
    }
}
