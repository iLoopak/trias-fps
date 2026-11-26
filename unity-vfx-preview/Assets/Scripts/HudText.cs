using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Trias
{
    internal sealed class HudText
    {
        readonly GUIContent content = new GUIContent();
        readonly Dictionary<(GUIStyle Style, int Width, string Text), string> lines = new Dictionary<(GUIStyle, int, string), string>(128);

        internal void Draw(Rect rect, string text, GUIStyle style)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text) || rect.width <= 2) return;
            content.text = style.wordWrap ? text : FitLine(text, Mathf.FloorToInt(rect.width - 2), style);
            GUI.Label(rect, content, style);
        }

        internal string FitLine(string text, int width, GUIStyle style)
        {
            var key = (style, width, text);
            if (lines.TryGetValue(key, out var fitted)) return fitted;
            if (lines.Count >= 256) lines.Clear();
            fitted = Measure(text, style) <= width ? text : Shorten(text, width, style);
            lines.Add(key, fitted);
            return fitted;
        }

        string Shorten(string text, int width, GUIStyle style)
        {
            const string ellipsis = "…";
            if (Measure(ellipsis, style) > width) return string.Empty;
            int[] starts = StringInfo.ParseCombiningCharacters(text);
            int low = 0, high = starts.Length - 1;
            string fitted = ellipsis;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                string candidate = text.Substring(0, starts[mid]).TrimEnd() + ellipsis;
                if (Measure(candidate, style) > width) high = mid - 1;
                else
                {
                    fitted = candidate;
                    low = mid + 1;
                }
            }
            return fitted;
        }

        float Measure(string text, GUIStyle style)
        {
            content.text = text;
            return style.CalcSize(content).x;
        }
    }
}
