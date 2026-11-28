using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Trias
{
    internal sealed class HudText
    {
        readonly GUIContent content = new GUIContent();
        readonly Dictionary<(GUIStyle Style, int Width, string Text), string> lines = new Dictionary<(GUIStyle, int, string), string>(128);

        static Texture2D backing;

        internal static void DrawBacking(Rect box)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (backing == null)
            {
                const int size = 32;
                backing = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { name = "HUD soft backing", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(x, size - 1 - x);
                    float feather = Mathf.SmoothStep(0, 1, (edge + 1) / 4f);
                    float alpha = Mathf.Lerp(.32f,.59f,y / (float)(size - 1)) * feather;
                    pixels[y * size + x] = new Color(.035f,.070f,.097f,alpha);
                }
                backing.SetPixels32(pixels);
                backing.Apply(false,true);
            }
            GUI.DrawTexture(box, backing);
        }

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

    // HUD lines rebuilt only when the values they show change. OnGUI runs several times per frame, and
    // concatenating "12 ms · room" or "1:05" every pass was the steady garbage that kept the GC busy.
    internal sealed class TextMemo
    {
        int a = int.MinValue, b = int.MinValue, c = int.MinValue;
        string s, text;

        internal string Of(int a, int b, int c, string s, Func<int, int, int, string, string> make)
        {
            if (text == null || a != this.a || b != this.b || c != this.c || !string.Equals(s, this.s))
            {
                this.a = a; this.b = b; this.c = c; this.s = s;
                text = make(a, b, c, s);
            }
            return text;
        }

        internal static TextMemo[] Row(int count)
        {
            var row = new TextMemo[count];
            for (int i = 0; i < count; i++) row[i] = new TextMemo();
            return row;
        }
    }
}
