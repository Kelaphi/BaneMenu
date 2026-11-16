using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BaneMenu.ui
{
    /// <summary>
    /// Tiny custom IMGUI toolkit: procedural textures, pill toggles, circular-knob sliders,
    /// buttons, dropdowns and text boxes. No asset files needed.
    /// </summary>
    public static class BaneGui
    {
        // ------------------------------------------------------------------ palette
        public static Color Accent = new Color(0.77f, 0.12f, 0.24f, 1f); // cardinal
        public static float Opacity = 1f;
        public static float BoundsH = 400f; // used to keep dropdown lists inside the window

        static readonly Color TextCol = new Color(0.93f, 0.93f, 0.94f, 1f);
        static readonly Color DimText = new Color(0.62f, 0.62f, 0.65f, 1f);
        static readonly Color TrackCol = new Color(0.078f, 0.078f, 0.078f, 1f);
        static readonly Color OffPill = new Color(0.078f, 0.078f, 0.078f, 1f);
        static readonly Color FieldBg = new Color(0.078f, 0.078f, 0.078f, 1f);
        static readonly Color ButtonBg = new Color(0.078f, 0.078f, 0.078f, 1f);
        static readonly Color ButtonBgHi = new Color(0.135f, 0.135f, 0.135f, 1f);
        static readonly Color PopupBg = new Color(0.10f, 0.10f, 0.10f, 1f);

        // ------------------------------------------------------------------ state
        static int _id;
        static int _active = -1;     // widget currently being pressed/dragged
        static int _popup = -1;      // dropdown currently open
        static int _popPickId = -1;  // dropdown that has a pending pick
        static int _popPick;
        static Rect _popHeader;
        static string[] _popOptions;
        static int _popSelected;
        static float _popScroll;
        static bool _popupAlive;
        static Rect _popHeaderScreen; // screen-space header for drawing outside scroll areas
        static readonly Dictionary<int, float> _anim = new Dictionary<int, float>();
        static readonly Dictionary<int, GUIStyle> _round = new Dictionary<int, GUIStyle>();

        // ------------------------------------------------------------------ textures / styles
        static Texture2D _circle, _glow, _tri, _window;
        static GUIStyle _label, _field;
        static GUIStyle _circleStyle, _glowStyle, _triStyle, _triUpStyle, _windowStyle, _whiteStyle;
        static Texture2D _white, _triUp;
        static readonly GUIContent _empty = new GUIContent("");

        public static void BeginFrame(int idBase)
        {
            _id = idBase;
            // Stale close is handled in EndFrame after widgets have marked themselves alive
            _popupAlive = false;
        }

        /// <summary>Call at end of OnGUI after DrawPopup. Closes popups whose dropdown was not redrawn.</summary>
        public static void EndFrame()
        {
            if (_popup != -1 && !_popupAlive)
            {
                _popup = -1;
                _popPickId = -1;
                _popOptions = null;
                _popScroll = 0f;
            }
        }
        public static void DismissPopupIfOutside()
        {
            // Inline dropdown handles outside-clicks while open; nothing to do here.
        }

        public static void ClosePopup()
        {
            _popup = -1;
            _popPickId = -1;
            _popOptions = null;
            _popScroll = 0f;
            _popListScreen = new Rect(0, 0, 0, 0);
        }

        /// <summary>True while a dropdown list is open.</summary>
        public static bool IsPopupId(int id) { return _popup == id; }
        public static bool PopupOpen { get { return _popup != -1; } }

        // Screen-space (Input.mousePosition / bottom-left origin) bounds of the open list
        static Rect _popListScreen;

        /// <summary>True if the system cursor is over the open dropdown list or its header.</summary>
        public static bool IsMouseOverPopup()
        {
            if (_popup == -1) return false;
            Vector2 sp = Input.mousePosition; // bottom-left origin
            if (_popListScreen.width > 0f && _popListScreen.Contains(sp)) return true;
            // Header in screen space (GUIToScreenPoint uses bottom-left Y)
            var hdr = new Rect(_popHeaderScreen.x, _popHeaderScreen.y, _popHeaderScreen.width, _popHeaderScreen.height);
            // GUIToScreenPoint returns bottom-left based coords but stored y is top of rect in screen space
            // Recompute using stored screen header - Unity stores y as bottom of GUI point's screen y
            if (hdr.width > 0f && sp.x >= hdr.x && sp.x <= hdr.x + hdr.width)
            {
                // header height in screen px
                float hy = hdr.y;
                float hh = hdr.height;
                // Depending on conversion, hdr.y may be bottom or top; cover both with a band
                if (sp.y >= hy - hh - 4f && sp.y <= hy + hh + 4f) return true;
            }
            return false;
        }
        public static bool Dragging { get { return _active != -1; } }

        public static void EnsureInit(int winW, int winH)
        {
            if (_circle != null && _label != null) return;

            _round.Clear();
            _circle = BuildCircle();
            _white = BuildWhite();
            _triUp = BuildTriangle(true);
            _tri = BuildTriangle(false);

            _circleStyle = TexStyle(_circle);
            _triStyle = TexStyle(_tri);
            _triUpStyle = TexStyle(_triUp);
            _whiteStyle = TexStyle(_white);

            _label = new GUIStyle(GUI.skin.label);
            _label.wordWrap = false;
            _label.richText = false;
            _label.clipping = TextClipping.Clip;
            _label.padding = Offset(0, 0, 0, 0);
            _label.margin = Offset(0, 0, 0, 0);

        }

        // The IL2CPP RectOffset has no 4-arg constructor, so set the fields by hand.
        static RectOffset Offset(int l, int r, int t, int b)
        {
            var o = new RectOffset();
            o.left = l; o.right = r; o.top = t; o.bottom = b;
            return o;
        }

        // Draw a texture stretched into a rect using a borderless GUIStyle.
        // (GUI.DrawTexture fails with "Method unstripping failed" in Among Us.)
        static GUIStyle TexStyle(Texture2D t)
        {
            var st = new GUIStyle();
            SetBg(st, t);
            st.border = Offset(0, 0, 0, 0);
            st.padding = Offset(0, 0, 0, 0);
            st.margin = Offset(0, 0, 0, 0);
            return st;
        }

        static void DrawStyled(Rect r, GUIStyle st, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.Box(r, _empty, st);
            GUI.color = prev;
        }

        static Texture2D BuildWhite()
        {
            var t = NewTex(4, 4, false);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    t.SetPixel(x, y, Color.white);
            t.Apply();
            return t;
        }

        static void SetBg(GUIStyle s, Texture2D t)
        {
            s.normal.background = t; s.hover.background = t; s.active.background = t; s.focused.background = t;
            s.onNormal.background = t; s.onHover.background = t; s.onActive.background = t; s.onFocused.background = t;
        }

        static void SetTextColor(GUIStyle s, Color c)
        {
            s.normal.textColor = c; s.hover.textColor = c; s.active.textColor = c; s.focused.textColor = c;
            s.onNormal.textColor = c; s.onHover.textColor = c; s.onActive.textColor = c; s.onFocused.textColor = c;
        }

        static Texture2D NewTex(int w, int h, bool mips)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // signed distance to a rounded box (negative = inside)
        static float Sdf(float px, float py, float w, float h, float r)
        {
            float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Texture2D BuildRounded(int r)
        {
            int s = 2 * r + 2;
            var t = NewTex(s, s, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float a = Mathf.Clamp01(0.5f - Sdf(x + 0.5f, y + 0.5f, s, s, r));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D BuildCircle()
        {
            const int s = 64;
            var t = NewTex(s, s, true);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s * 0.5f, dy = y + 0.5f - s * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - (s * 0.5f - 0.5f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
                }
            t.Apply(true);
            return t;
        }

        static Texture2D BuildGlow()
        {
            const int s = 128;
            var t = NewTex(s, s, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f - s * 0.5f) / (s * 0.5f), dy = (y + 0.5f - s * 0.5f) / (s * 0.5f);
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = (1f - d); a *= a;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        // down-pointing triangle (dropdown chevron)
        static Texture2D BuildTriangle(bool up)
        {
            const int s = 32;
            var t = NewTex(s, s, true);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                    if (up) v = 1f - v;
                    float side = (v * 0.5f - Mathf.Abs(u - 0.5f)) * s / 1.118f;
                    float top = (1f - v) * s;
                    float a = Mathf.Clamp01(Mathf.Min(side, top) + 0.5f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply(true);
            return t;
        }

        // the textured window background: gradient + hatch + grain + vignette + rim, rounded corners
        static Texture2D BuildWindow(int fw, int fh)
        {
            int w = fw / 2, h = fh / 2; // half-res, stretched with bilinear filtering
            var t = NewTex(w, h, false);
            var rng = new System.Random(1337);
            var top = new Color(0.118f, 0.110f, 0.188f, 1f);
            var bot = new Color(0.050f, 0.050f, 0.085f, 1f);
            var rim = new Color(0.32f, 0.32f, 0.48f, 1f);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Sdf(x + 0.5f, y + 0.5f, w, h, 7f);
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a <= 0f) { t.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                    Color c = Color.Lerp(bot, top, y / (float)(h - 1));
                    float v = 0f;
                    if (((x + y) % 5) == 0) v += 0.020f;                    // diagonal hatch
                    if ((((x - y) % 24) + 24) % 24 == 0) v += 0.012f;       // faint cross hatch
                    v += ((float)rng.NextDouble() - 0.5f) * 0.035f;         // grain
                    float nx = (x / (float)w - 0.5f) * 2f, ny = (y / (float)h - 0.5f) * 2f;
                    v -= 0.025f * (nx * nx + ny * ny);                      // vignette
                    c.r += v; c.g += v; c.b += v;
                    if (d > -0.9f) c = Color.Lerp(c, rim, 0.85f);           // rim light
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static GUIStyle RoundStyle(int r)
        {
            GUIStyle s;
            if (_round.TryGetValue(r, out s) && s != null) return s;
            s = new GUIStyle();
            SetBg(s, BuildRounded(r));
            s.border = Offset(r, r, r, r);
            _round[r] = s;
            return s;
        }

        // ------------------------------------------------------------------ primitives
        public static void Rounded(Rect r, Color c, int radius)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.Box(r, _empty, RoundStyle(radius));
            GUI.color = prev;
        }

        public static void Fill(Rect r, Color c)
        {
            DrawStyled(r, _whiteStyle, c);
        }

        public static void Circle(Rect r, Color c)
        {
            DrawStyled(r, _circleStyle, c);
        }

        static void Chevron(Rect r, bool up, Color c)
        {
            DrawStyled(r, up ? _triUpStyle : _triStyle, c);
        }

        public static void Label(Rect r, string text, int size, Color col,
                                 TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            _label.fontSize = size;
            _label.alignment = anchor;
            _label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            _label.normal.textColor = col;
            GUI.Label(r, text, _label);
        }

        public static void Section(Rect r, string text)
        {
            Label(r, text.ToUpperInvariant(), 11, DimText, TextAnchor.MiddleLeft, true);
        }

        public static void Shadow(Rect win)
        {
            Rounded(new Rect(win.x - 3f, win.y - 1f, win.width + 6f, win.height + 8f), new Color(0f, 0f, 0f, 0.22f), 12);
        }

        // flat #262626 window with soft rounded corners (call inside the window's group)
        public static void DrawWindowBg(Rect r)
        {
            // Radius 10 - slightly larger than content inset so all four corners stay rounded
            // even when opaque child panes approach the edges.
            Rounded(r, new Color(0.149f, 0.149f, 0.149f, Opacity), 10);
        }

        // ------------------------------------------------------------------ helpers
        static int NextId() { return ++_id; }

        static bool Over(Rect r, int id)
        {
            return _popup == -1 && (_active == -1 || _active == id) && r.Contains(Event.current.mousePosition);
        }

        static float Anim(int id, float target)
        {
            float v;
            if (!_anim.TryGetValue(id, out v)) v = target;
            if (Event.current.type == EventType.Repaint)
                v = Mathf.MoveTowards(v, target, Time.unscaledDeltaTime * 8f);
            _anim[id] = v;
            return v * v * (3f - 2f * v); // smoothstep
        }

        /// <summary>Press/release behaviour for a rect. Returns true on click.</summary>
        public static bool Behave(Rect r, out bool hover, out bool pressed)
        {
            int id = NextId();
            var e = Event.current;
            hover = Over(r, id);
            pressed = _active == id;
            bool clicked = false;

            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                _active = id;
                pressed = true;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _active == id)
            {
                clicked = r.Contains(e.mousePosition);
                _active = -1;
                pressed = false;
                e.Use();
            }
            return clicked;
        }

        // ------------------------------------------------------------------ widgets
        public static bool Toggle(Rect r, string label, bool value)
        {
            int id = NextId();
            var e = Event.current;
            bool hover = Over(r, id);

            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                value = !value;
                e.Use();
            }

            float t = Anim(id, value ? 1f : 0f);
            float h = Anim(id + 500000, hover ? 1f : 0f);

            var pill = new Rect(r.x + 3f, r.y + (r.height - 20f) * 0.5f, 40f, 20f);
            Color offc = Color.Lerp(OffPill, new Color(0.135f, 0.135f, 0.135f, 1f), h);
            Color onc = new Color(Accent.r * 0.51f, Accent.g * 0.36f, Accent.b * 0.52f, 1f);
            Rounded(pill, Color.Lerp(offc, onc, t), 10);

            float kx = Mathf.Lerp(pill.x, pill.xMax - 20f, t);
            Circle(new Rect(kx, pill.y, 20f, 20f), Accent);
            Label(new Rect(r.x + 47f, r.y, r.width - 47f, r.height), label, 13, TextCol);
            return value;
        }

        public static bool Button(Rect r, string label, bool accent)
        {
            int idForAnim = _id + 1; // Behave() will take this id
            bool hover, pressed;
            bool clicked = Behave(r, out hover, out pressed);
            float t = Anim(idForAnim, hover ? 1f : 0f);

            Color baseC = new Color(Accent.r * 0.96f, Accent.g * 0.94f, Accent.b * 0.92f, 1f);
            Color c = Color.Lerp(baseC, Color.Lerp(baseC, Color.white, 0.18f), t);
            if (pressed) c = new Color(c.r * 0.78f, c.g * 0.78f, c.b * 0.78f, c.a);

            Rounded(r, c, (int)(r.height * 0.5f));
            Label(r, label, 13, Color.white, TextAnchor.MiddleCenter, false);
            return clicked;
        }

        public static float Slider(Rect r, string text, float v, float min, float max)
        {
            int id = NextId();
            var e = Event.current;

            var track = new Rect(r.x + 3f, r.y + 30f, r.width - 9f, 10f);
            var hit = new Rect(r.x, r.y + 22f, r.width, 26f);
            bool hover = Over(hit, id);

            // input first so the frame we draw already has the new value
            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                _active = id;
                v = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                e.Use();
            }
            else if (_active == id && e.type == EventType.MouseDrag)
            {
                v = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                e.Use();
            }
            else if (_active == id && e.type == EventType.MouseUp)
            {
                _active = -1;
                e.Use();
            }

            float k = Anim(id, (hover || _active == id) ? 1f : 0f);
            float t = Mathf.InverseLerp(min, max, v);

            Label(new Rect(r.x, r.y, r.width, 18f), text, 13, TextCol);
            Rounded(track, TrackCol, 5);

            float cx = track.x + 10f + (track.width - 20f) * t, cy = track.y + 5f;
            float d = 20f + 2f * k;
            Circle(new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d), Accent);
            return v;
        }


        // Open dropdown id + scroll for inline list
        static float _inlineScroll;

        /// <summary>
        /// Inline expandable dropdown (no overlay/window). Clicks and scroll work
        /// because everything stays in the same GUILayout/IMGUI flow.
        /// </summary>
        public static int Dropdown(Rect r, string label, string[] options, int selected, string stableKey = null)
        {
            if (options == null || options.Length == 0) return 0;

            int id = string.IsNullOrEmpty(stableKey)
                ? NextId()
                : (stableKey.GetHashCode() & 0x7fffffff) + 800000;
            if (!string.IsNullOrEmpty(stableKey)) NextId(); // keep id sequence stable for other widgets
            var e = Event.current;

            if (_popPickId == id)
            {
                selected = _popPick;
                _popPickId = -1;
            }
            selected = Mathf.Clamp(selected, 0, options.Length - 1);

            float lw = r.width * 0.42f;
            Label(new Rect(r.x, r.y, lw, r.height), label, 13, TextCol);
            var box = new Rect(r.x + lw, r.y, r.width - lw, r.height);

            bool open = _popup == id;
            bool hover = box.Contains(e.mousePosition);
            float t = Anim(id, (hover || open) ? 1f : 0f);

            Rounded(box, Color.Lerp(ButtonBg, ButtonBgHi, t), 8);
            Label(new Rect(box.x + 12f, box.y, box.width - 38f, box.height), options[selected], 13, TextCol);
            Chevron(new Rect(box.xMax - 26f, box.y + (box.height - 8f) * 0.5f, 12f, 8f),
                    open, open ? Accent : DimText);

            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                if (_popup == id) ClosePopup();
                else
                {
                    _popup = id;
                    _popOptions = options;
                    _popSelected = selected;
                    _popScroll = 0f;
                    _popupAlive = true;
                    _popHeader = box;
                }
                e.Use();
            }

            // ---- inline list directly under the header (same coordinate space)
            if (open)
            {
                _popupAlive = true;
                _popOptions = options;
                _popSelected = selected;
                _popHeader = box;

                const float itemH = 26f;
                float maxH = Mathf.Min(options.Length * itemH + 6f, itemH * 7f + 6f);
                var list = new Rect(box.x, box.yMax + 3f, box.width, maxH);

                // Background
                Rounded(new Rect(list.x - 1f, list.y - 1f, list.width + 2f, list.height + 2f),
                        new Color(0f, 0f, 0f, 0.35f), 9);
                Rounded(list, PopupBg, 8);

                // Wheel scroll when over the list
                if (list.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
                {
                    float contentH = options.Length * itemH + 6f;
                    float maxScroll = Mathf.Max(0f, contentH - maxH);
                    _popScroll = Mathf.Clamp(_popScroll + e.delta.y * 16f, 0f, maxScroll);
                    e.Use();
                }

                // Clip items to list bounds
                GUI.BeginGroup(list);
                try
                {
                    for (int i = 0; i < options.Length; i++)
                    {
                        var item = new Rect(4f, 3f + i * itemH - _popScroll, list.width - 8f, itemH - 2f);
                        if (item.yMax < 0f || item.y > list.height) continue;

                        bool over = item.Contains(e.mousePosition);
                        if (over)
                            Rounded(item, new Color(Accent.r, Accent.g, Accent.b, 0.35f), 6);

                        bool sel = i == selected;
                        Label(new Rect(item.x + 8f, item.y, item.width - 16f, item.height),
                              options[i], 13, sel ? Accent : TextCol, TextAnchor.MiddleLeft, sel);

                        // Primary click path
                        if (e.type == EventType.MouseDown && e.button == 0 && over)
                        {
                            _popPickId = id;
                            _popPick = i;
                            selected = i;
                            ClosePopup();
                            e.Use();
                        }
                        // Backup: invisible button
                        if (GUI.Button(item, GUIContent.none, GUIStyle.none))
                        {
                            _popPickId = id;
                            _popPick = i;
                            selected = i;
                            ClosePopup();
                        }
                    }
                }
                finally { GUI.EndGroup(); }

                // Scrollbar
                float contentH2 = options.Length * itemH + 6f;
                if (contentH2 > maxH + 1f)
                {
                    float ratio = maxH / contentH2;
                    float thumbH = Mathf.Max(10f, list.height * ratio);
                    float maxScroll = contentH2 - maxH;
                    float thumbY = list.y + (list.height - thumbH) * (_popScroll / Mathf.Max(1f, maxScroll));
                    Rounded(new Rect(list.xMax - 5f, thumbY, 3f, thumbH), DimText, 2);
                }

                // Mark list screen rect for ClickBlocker
                Vector2 s0 = GUIUtility.GUIToScreenPoint(new Vector2(list.x, list.y));
                Vector2 s1 = GUIUtility.GUIToScreenPoint(new Vector2(list.xMax, list.yMax));
                _popListScreen = new Rect(
                    Mathf.Min(s0.x, s1.x) - 2f,
                    Mathf.Min(s0.y, s1.y) - 2f,
                    Mathf.Abs(s1.x - s0.x) + 4f,
                    Mathf.Abs(s1.y - s0.y) + 4f);

                // Click outside header+list closes (same event)
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (!list.Contains(e.mousePosition) && !box.Contains(e.mousePosition))
                        ClosePopup();
                }
            }

            return selected;
        }

        public static void HandlePopupInput() { }

        /// <summary>No overlay - list is drawn inside Dropdown().</summary>
        public static void DrawPopup() { }

        
        // ------------------------------------------------------------------ text box
        // Fully custom: GUI.TextField / FocusControl / GetNameOfFocusedControl are stripped
        // in Among Us' IL2CPP build (they threw, which also stopped everything drawn after them).
        static int _focusId = -1;
        static int _caret;
        static int _selAnchor = -1; // selection anchor; -1 = no selection (caret only)
        static bool _mouseSelecting;
        static string _pendingInput = "";
        static bool _charApiBroken;

        public static bool TextFocused { get { return _focusId != -1; } }
        public static bool CharApiBroken { get { return _charApiBroken; } }
        public static void Unfocus() { _focusId = -1; _selAnchor = -1; _mouseSelecting = false; }

        /// <summary>Fallback typing source (Input.inputString) for when Event.character is unavailable.</summary>
        public static void FeedInput(string str)
        {
            if (_focusId != -1 && !string.IsNullOrEmpty(str)) _pendingInput += str;
        }

        static float TextWidth(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0f;
            try
            {
                _label.fontSize = 13;
                _label.fontStyle = FontStyle.Normal;
                return _label.CalcSize(new GUIContent(str)).x;
            }
            catch { return str.Length * 7f; }
        }

        static void GetSelRange(int len, out int a, out int b)
        {
            int c = Mathf.Clamp(_caret, 0, len);
            if (_selAnchor < 0) { a = b = c; return; }
            int s = Mathf.Clamp(_selAnchor, 0, len);
            a = Mathf.Min(s, c);
            b = Mathf.Max(s, c);
        }

        static bool HasSelection(int len)
        {
            GetSelRange(len, out int a, out int b);
            return b > a;
        }

        static void ClearSelection() { _selAnchor = -1; }

        static void DeleteSelection(ref string text)
        {
            GetSelRange(text.Length, out int a, out int b);
            if (b <= a) return;
            text = text.Remove(a, b - a);
            _caret = a;
            ClearSelection();
        }

        static string SelectedText(string text)
        {
            GetSelRange(text.Length, out int a, out int b);
            if (b <= a) return "";
            return text.Substring(a, b - a);
        }

        static float ScrollFor(string text, float innerW)
        {
            int c = Mathf.Clamp(_caret, 0, text.Length);
            float cx = TextWidth(text.Substring(0, c));
            return Mathf.Max(0f, cx - (innerW - 4f));
        }

        static int CaretFromMouseX(string text, float relX)
        {
            int best = text.Length;
            float bestD = float.MaxValue;
            for (int i = 0; i <= text.Length; i++)
            {
                float dd = Mathf.Abs(TextWidth(text.Substring(0, i)) - relX);
                if (dd < bestD) { bestD = dd; best = i; }
            }
            return best;
        }

        static void InsertChar(ref string text, char ch, int maxLen)
        {
            if (char.IsControl(ch)) return;
            if (HasSelection(text.Length)) DeleteSelection(ref text);
            if (text.Length >= maxLen) return;
            _caret = Mathf.Clamp(_caret, 0, text.Length);
            text = text.Insert(_caret, ch.ToString());
            _caret++;
            ClearSelection();
        }

        static void HandleKey(Event e, ref string text, int maxLen, ref bool committed)
        {
            KeyCode kc;
            char ch;
            bool ctrl;
            bool shift;
            try
            {
                kc = e.keyCode;
                ch = e.character;
                ctrl = e.control || e.command;
                shift = e.shift;
            }
            catch { _charApiBroken = true; return; }

            _caret = Mathf.Clamp(_caret, 0, text.Length);

            // Ctrl shortcuts
            if (ctrl)
            {
                switch (kc)
                {
                    case KeyCode.A:
                        _selAnchor = 0;
                        _caret = text.Length;
                        e.Use(); return;
                    case KeyCode.C:
                        try { GUIUtility.systemCopyBuffer = SelectedText(text); } catch { }
                        e.Use(); return;
                    case KeyCode.X:
                        try { GUIUtility.systemCopyBuffer = SelectedText(text); } catch { }
                        if (HasSelection(text.Length)) DeleteSelection(ref text);
                        e.Use(); return;
                    case KeyCode.V:
                        {
                            string clip = null;
                            try { clip = GUIUtility.systemCopyBuffer; } catch { }
                            if (!string.IsNullOrEmpty(clip))
                            {
                                if (HasSelection(text.Length)) DeleteSelection(ref text);
                                foreach (char c in clip) InsertChar(ref text, c, maxLen);
                            }
                            e.Use();
                            return;
                        }
                }
            }

            switch (kc)
            {
                case KeyCode.Backspace:
                    if (HasSelection(text.Length)) DeleteSelection(ref text);
                    else if (_caret > 0) { text = text.Remove(_caret - 1, 1); _caret--; ClearSelection(); }
                    e.Use(); return;
                case KeyCode.Delete:
                    if (HasSelection(text.Length)) DeleteSelection(ref text);
                    else if (_caret < text.Length) { text = text.Remove(_caret, 1); ClearSelection(); }
                    e.Use(); return;
                case KeyCode.LeftArrow:
                    if (shift)
                    {
                        if (_selAnchor < 0) _selAnchor = _caret;
                        _caret = Mathf.Max(0, _caret - 1);
                    }
                    else
                    {
                        if (HasSelection(text.Length)) { GetSelRange(text.Length, out int a, out _); _caret = a; }
                        else _caret = Mathf.Max(0, _caret - 1);
                        ClearSelection();
                    }
                    e.Use(); return;
                case KeyCode.RightArrow:
                    if (shift)
                    {
                        if (_selAnchor < 0) _selAnchor = _caret;
                        _caret = Mathf.Min(text.Length, _caret + 1);
                    }
                    else
                    {
                        if (HasSelection(text.Length)) { GetSelRange(text.Length, out _, out int b); _caret = b; }
                        else _caret = Mathf.Min(text.Length, _caret + 1);
                        ClearSelection();
                    }
                    e.Use(); return;
                case KeyCode.Home:
                    if (shift) { if (_selAnchor < 0) _selAnchor = _caret; }
                    else ClearSelection();
                    _caret = 0;
                    e.Use(); return;
                case KeyCode.End:
                    if (shift) { if (_selAnchor < 0) _selAnchor = _caret; }
                    else ClearSelection();
                    _caret = text.Length;
                    e.Use(); return;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    committed = true; e.Use(); return;
                case KeyCode.Escape:
                    _focusId = -1; ClearSelection(); e.Use(); return;
            }

            if (ch != '\0')
            {
                InsertChar(ref text, ch, maxLen);
                e.Use();
            }
        }

        public static string TextBox(Rect r, string label, string text, string name,
                                     out bool committed, int maxLen)
        {
            committed = false;
            // Stable id from name so focus survives across frames (NextId resets each BeginFrame)
            int id = string.IsNullOrEmpty(name) ? NextId() : (name.GetHashCode() & 0x7fffffff) + 900000;
            NextId(); // keep sequence moving for other widgets
            var e = Event.current;
            if (text == null) text = "";

            float lw = r.width * 0.42f;
            Label(new Rect(r.x, r.y, lw, r.height), label, 13, TextCol);
            var box = new Rect(r.x + lw, r.y, r.width - lw, r.height);
            float innerW = box.width - 16f;

            bool focused = _focusId == id;
            if (focused) _caret = Mathf.Clamp(_caret, 0, text.Length);

            if (_popup == -1)
            {
                // Mouse: focus, place caret, drag-select
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (box.Contains(e.mousePosition) && _active == -1)
                    {
                        float scroll0 = focused ? ScrollFor(text, innerW) : 0f;
                        float relX = e.mousePosition.x - (box.x + 8f) + scroll0;
                        int best = CaretFromMouseX(text, relX);
                        _focusId = id;
                        focused = true;
                        if (e.shift)
                        {
                            if (_selAnchor < 0) _selAnchor = _caret;
                            _caret = best;
                        }
                        else
                        {
                            _caret = best;
                            _selAnchor = best;
                            _mouseSelecting = true;
                        }
                        e.Use();
                    }
                    else if (focused)
                    {
                        _focusId = -1;
                        focused = false;
                        ClearSelection();
                        _mouseSelecting = false;
                    }
                }
                else if (focused && _mouseSelecting && (e.type == EventType.MouseDrag || e.type == EventType.MouseMove))
                {
                    float scroll0 = ScrollFor(text, innerW);
                    float relX = e.mousePosition.x - (box.x + 8f) + scroll0;
                    _caret = CaretFromMouseX(text, relX);
                    e.Use();
                }
                else if (focused && _mouseSelecting && e.type == EventType.MouseUp)
                {
                    GetSelRange(text.Length, out int a, out int b);
                    if (a == b) ClearSelection();
                    _mouseSelecting = false;
                    e.Use();
                }

                if (focused)
                {
                    if (e.type == EventType.Layout && _pendingInput.Length > 0)
                    {
                        _caret = Mathf.Clamp(_caret, 0, text.Length);
                        foreach (char c in _pendingInput)
                        {
                            if (c == '\b')
                            {
                                if (HasSelection(text.Length)) DeleteSelection(ref text);
                                else if (_caret > 0) { text = text.Remove(_caret - 1, 1); _caret--; ClearSelection(); }
                            }
                            else if (c == '\n' || c == '\r') committed = true;
                            else InsertChar(ref text, c, maxLen);
                        }
                        _pendingInput = "";
                    }

                    if (e.type == EventType.KeyDown) HandleKey(e, ref text, maxLen, ref committed);
                    focused = _focusId == id;
                }
            }

            if (committed) { _focusId = -1; focused = false; ClearSelection(); _mouseSelecting = false; }

            // ---- draw
            if (focused) Rounded(new Rect(box.x - 1.5f, box.y - 1.5f, box.width + 3f, box.height + 3f), Accent, 9);
            Rounded(box, focused ? new Color(0.05f, 0.05f, 0.05f, 1f) : FieldBg, 8);

            float scroll = focused ? ScrollFor(text, innerW) : 0f;
            GUI.BeginGroup(new Rect(box.x + 8f, box.y, innerW, box.height));
            try
            {
                if (focused && HasSelection(text.Length))
                {
                    GetSelRange(text.Length, out int sa, out int sb);
                    float x0 = TextWidth(text.Substring(0, sa)) - scroll;
                    float x1 = TextWidth(text.Substring(0, sb)) - scroll;
                    Fill(new Rect(x0, 3f, Mathf.Max(1f, x1 - x0), box.height - 6f),
                        new Color(Accent.r, Accent.g, Accent.b, 0.45f));
                }

                Label(new Rect(-scroll, 0f, 4000f, box.height), text, 13, Color.white);
                if (focused && !HasSelection(text.Length) && (((int)(Time.realtimeSinceStartup * 2f)) % 2 == 0))
                {
                    float cx = TextWidth(text.Substring(0, Mathf.Clamp(_caret, 0, text.Length))) - scroll;
                    Fill(new Rect(cx, 5f, 1.5f, box.height - 10f), Color.white);
                }
            }
            finally { GUI.EndGroup(); }

            return text;
        }
    }
}
