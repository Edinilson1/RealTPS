using System.Collections.Generic;
using UnityEngine;

namespace RealTPS
{
    /// <summary>
    /// Colours and IMGUI styles of the shield window: a dark palette with flat, rounded panels.
    /// </summary>
    internal sealed class ShieldWindowStyle
    {
        public static readonly Color Background = new Color(0.075f, 0.082f, 0.105f, 0.985f);
        public static readonly Color Panel = new Color(0.12f, 0.13f, 0.16f, 1f);
        public static readonly Color Border = new Color(0.23f, 0.24f, 0.29f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.93f, 0.96f, 1f);
        public static readonly Color Muted = new Color(0.60f, 0.65f, 0.72f, 1f);
        public static readonly Color Accent = new Color(1f, 0.447f, 0.247f, 1f);
        public static readonly Color Blue = new Color(0.176f, 0.584f, 0.937f, 1f);
        public static readonly Color Good = new Color(0.22f, 0.78f, 0.49f, 1f);
        public static readonly Color Caution = new Color(0.98f, 0.76f, 0.24f, 1f);
        public static readonly Color Warning = new Color(1f, 0.42f, 0.16f, 1f);
        public static readonly Color Danger = new Color(0.94f, 0.16f, 0.17f, 1f);
        public static readonly Color Gone = new Color(0.22f, 0.23f, 0.26f, 1f);

        private readonly List<Texture2D> fills = new List<Texture2D>();

        public GUIStyle Window;
        public GUIStyle PanelBox;
        public GUIStyle Chip;
        public GUIStyle Action;
        public GUIStyle ActiveTab;
        public GUIStyle TextStyle;
        public GUIStyle Label;
        public GUIStyle LabelRight;
        public GUIStyle Small;
        public GUIStyle Title;
        public GUIStyle Big;
        public GUIStyle Word;
        public GUIStyle Centered;
        public GUIStyle Marker;
        public GUIStyle ChipLabel;
        public GUIStyle ChipCount;

        public bool Built
        {
            get { return TextStyle != null; }
        }

        /// <summary>Builds every style from the current skin. Call inside OnGUI with the skin in place.</summary>
        public void Build()
        {
            Destroy();
            TextStyle = new GUIStyle(GUI.skin.label) { wordWrap = false, fontSize = 14 };
            TextStyle.normal.textColor = Text;
            Label = new GUIStyle(TextStyle) { fontSize = 11, wordWrap = true };
            Label.normal.textColor = Muted;
            LabelRight = new GUIStyle(Label) { alignment = TextAnchor.UpperRight, wordWrap = false };
            Small = new GUIStyle(TextStyle) { fontSize = 12, wordWrap = true };
            Title = new GUIStyle(TextStyle) { fontSize = 17, fontStyle = FontStyle.Bold };
            Big = new GUIStyle(Title) { fontSize = 24 };
            Word = new GUIStyle(Title) { fontSize = 16 };
            Centered = new GUIStyle(Title) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            Marker = new GUIStyle(TextStyle) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ChipLabel = new GUIStyle(Label) { fontSize = 10, wordWrap = false };
            ChipCount = new GUIStyle(Title) { fontSize = 17 };

            Window = new GUIStyle(GUI.skin.window) { border = new RectOffset(3, 3, 3, 3) };
            Window.normal.background = Fill(Background);
            Window.onNormal.background = Window.focused.background = Window.onFocused.background = Window.normal.background;
            PanelBox = new GUIStyle(GUI.skin.box) { border = new RectOffset(3, 3, 3, 3) };
            PanelBox.normal.background = Fill(Panel);
            Chip = new GUIStyle(GUI.skin.box) { border = new RectOffset(3, 3, 3, 3) };
            Chip.normal.background = Fill(Color.Lerp(Panel, Border, 0.55f));

            Action = new GUIStyle(GUI.skin.button) { fontSize = 12, border = new RectOffset(3, 3, 3, 3) };
            Action.normal.background = Fill(Border);
            Action.hover.background = Fill(Color.Lerp(Border, Blue, 0.4f));
            Action.active.background = Fill(Blue);
            Action.focused.background = Action.normal.background;
            Action.normal.textColor = Action.hover.textColor = Action.active.textColor = Action.focused.textColor = Text;
            ActiveTab = new GUIStyle(Action);
            ActiveTab.normal.background = ActiveTab.focused.background = Fill(Accent);
            ActiveTab.normal.textColor = ActiveTab.focused.textColor = Color.black;
        }

        /// <summary>A flat fill with the four corner pixels cut, which reads as a slightly rounded panel.</summary>
        private Texture2D Fill(Color colour)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var colours = new Color[64];
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    colours[y * 8 + x] = (x == 0 || x == 7) && (y == 0 || y == 7) ? Color.clear : colour;
                }
            }
            texture.SetPixels(colours);
            texture.Apply(false, true);
            fills.Add(texture);
            return texture;
        }

        public void Destroy()
        {
            for (int i = 0; i < fills.Count; i++)
            {
                if (fills[i] != null)
                {
                    Object.Destroy(fills[i]);
                }
            }
            fills.Clear();
        }

        /// <summary>Colour of a 0..1 condition (integrity, reserve, structure).</summary>
        public static Color Condition(double value)
        {
            if (double.IsNaN(value))
            {
                return Muted;
            }
            return value <= 0.0 ? Gone : value < 0.2 ? Danger : value < 0.4 ? Warning : value < 0.7 ? Caution : Good;
        }

        /// <summary>A thin progress bar: the track in the border colour, the filled share in <paramref name="colour"/>.</summary>
        public static void Bar(Rect rect, double fraction, Color colour)
        {
            Color old = GUI.color;
            GUI.color = Border;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            if (!double.IsNaN(fraction) && fraction > 0.0)
            {
                GUI.color = colour;
                rect.width *= (float)(fraction > 1.0 ? 1.0 : fraction);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        /// <summary>Draws text in a colour, restoring the style afterwards.</summary>
        public static void Coloured(Rect rect, string text, GUIStyle style, Color colour)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = colour;
            GUI.Label(rect, text, style);
            style.normal.textColor = old;
        }

        /// <summary>Text with a one-pixel dark shadow, readable on any map colour.</summary>
        public static void Shadowed(Rect rect, string text, GUIStyle style, Color colour)
        {
            Coloured(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style, new Color(0f, 0f, 0f, 0.85f));
            Coloured(rect, text, style, colour);
        }
    }
}
