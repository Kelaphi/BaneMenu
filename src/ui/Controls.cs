using AmongUs.GameOptions;
using BaneMenu.assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BaneMenu.ui
{
	internal class Controls
	{
		// The RoleTypes enum has some weird gaps, everything from Crewmate (0) to Tracker (10) is normal, but then Detective is 12, Viper is 18, and SpiritGuide (Influencer) is 21
		// https://www.innersloth.com/2026-roadmap-part-1/
		// The Among Us 2026 roadmap does state that there are currently 15 prototype roles in the works,
		// could these gaps be attributed to roles that have not been added to the retail version of the game?
		public static readonly List<RoleTypes> RolesList = new List<RoleTypes>()
		{
			RoleTypes.Crewmate,
			RoleTypes.Scientist,
			RoleTypes.Engineer,
			RoleTypes.Noisemaker,
			RoleTypes.Tracker,
			RoleTypes.Detective,
			RoleTypes.Judge,
			RoleTypes.Impostor,
			RoleTypes.Shapeshifter,
			RoleTypes.Phantom,
			RoleTypes.Viper,
			RoleTypes.CrewmateGhost,
			RoleTypes.GuardianAngel,
			RoleTypes.SpiritGuide,
			RoleTypes.ImpostorGhost
		};

		public enum PlayerColors
		{
			Red,
			Blue,
			Green,
			Pink,
			Orange,
			Yellow,
			Black,
			White,
			Purple,
			Brown,
			Cyan,
			Lime,
			Maroon,
			Rose,
			Banana,
			Gray,
			Tan,
			Coral,
			Fortegreen
		}

		
		public static T HorizontalListSlider<T>(List<T> list, T currentValue)
		{
			int index = list.IndexOf(currentValue);
			if(index < 0) index = 0;

			int newValue = (int)Controls.HorizontalSlider(index, 0, Math.Max(0, list.Count - 1));

			return list[newValue];
		}

		public static TKey HorizontalListSlider<TKey, TValue>(SortedList<TKey, TValue> list, TKey currentValue)
		{
			int index = list.IndexOfKey(currentValue);
			if(index < 0) index = 0;

			int newIndex = (int)Controls.HorizontalSlider(index, 0, Math.Max(0, list.Count - 1));

			return list.Keys[newIndex];
		}

		public static RoleTypes HorizontalRoleSlider(RoleTypes currentRole)
		{
			return HorizontalListSlider(GameAssets.RolesList, currentRole);
		}

		public static PlayerColors HorizontalColorSlider(PlayerColors currentColor)
		{
			return (PlayerColors)Controls.HorizontalSlider((int)currentColor, 0, Palette.ColorNames.Length);
		}

		public static int HorizontalVentSlider(SortedDictionary<int, string> vents, int currentVent)
		{
			// Reset current vent if it is out ouf bounds
			int firstKey = vents.Keys.First();
			if(currentVent < firstKey || currentVent >= vents.Count) currentVent = firstKey;

			return (int)Controls.HorizontalSlider(currentVent, firstKey, vents.Count - 1);
		}

		public static bool PlayerSpecificToggle(string label, PlayerControl selectedPlayer, ref PlayerControl currentPlayer)
		{
			bool isCurrentSelection = selectedPlayer != null && selectedPlayer == currentPlayer;
			string key = "pst_" + label;
			GUILayout.BeginHorizontal();
			if (DrawPillToggleButton(key, isCurrentSelection))
				currentPlayer = isCurrentSelection ? null : selectedPlayer;
			GUILayout.Space(PadS());
			GUILayout.Label(label, PillToggleLabelStyle());
			GUILayout.EndHorizontal();
			GUILayout.Space(PadS());
			return currentPlayer != null;
		}

		public static bool PlayerSpecificToggle(string label, PlayerControl selectedPlayer, HashSet<int> currentPlayers)
		{
			bool isSelected = selectedPlayer != null && currentPlayers.Contains(selectedPlayer.GetHashCode());
			string key = "psth_" + label + "_" + (selectedPlayer != null ? selectedPlayer.GetHashCode() : 0);
			GUILayout.BeginHorizontal();
			if (DrawPillToggleButton(key, isSelected) && selectedPlayer != null)
			{
				if (!isSelected)
					currentPlayers.Add(selectedPlayer.GetHashCode());
				else
					currentPlayers.Remove(selectedPlayer.GetHashCode());
			}
			GUILayout.Space(PadS());
			GUILayout.Label(label, PillToggleLabelStyle());
			GUILayout.EndHorizontal();
			GUILayout.Space(PadS());
			return currentPlayers.Count != 0;
		}

		public static bool GlobalPlayerSpecificToggle(string label, HashSet<int> currentPlayers)
		{
			bool enabled = currentPlayers.Count != 0;
			string key = "gpst_" + label;
			GUILayout.BeginHorizontal();
			if (DrawPillToggleButton(key, enabled))
			{
				if (!enabled)
					currentPlayers.Add(int.MaxValue);
				else
					currentPlayers.Clear();
			}
			GUILayout.Space(PadS());
			GUILayout.Label(label, PillToggleLabelStyle());
			GUILayout.EndHorizontal();
			GUILayout.Space(PadS());
			return currentPlayers.Count != 0;
		}

		public static void DrawCrewmateColorBox(Rect rect, NetworkedPlayerInfo player)
		{
			string colorName = Utilities.GetPlayerColor(player);
			GUI.Box(rect, "", Styles.CreateCrewmateColorBox(colorName, colorName != "Fortegreen" ? player.Color : Color.black));
		}

		public static void DrawButtonCell<TKey, TValue>(Dictionary<TKey, TValue> buttons, Action<TValue> action, int columnsPerRow)
		{
			int currentColumn = 0;

			foreach((TKey key, TValue value) in buttons)
			{
				if(currentColumn == 0)
				{
					GUILayout.BeginHorizontal();
				}

				if(Button(key.ToString()))
				{
					action(value);
				}

				currentColumn++;
				if(currentColumn == columnsPerRow)
				{
					GUILayout.EndHorizontal();
					currentColumn = 0;
				}
			}

			// If the amount of buttons does not divide into the amount of columns per row then we won't be ending the horizontal layout
			// so we check if we need to end it here
			if(currentColumn != 0)
			{
				GUILayout.EndHorizontal();
			}
		}
		
		// ---- New-design widgets (equal padding on every side) ----
		// Outer gap between widgets and inner pad inside each control share the same value.
		private const float WidgetPad = 3f;

		private static readonly Dictionary<string, float> ToggleAnimations = new Dictionary<string, float>();
		private static readonly Dictionary<string, float> SliderHoverAnims = new Dictionary<string, float>();
		private static int _sliderSeq;
		private static int _activeSliderId = -1;

		/// <summary>Call once at the start of each OnGUI frame before drawing widgets.</summary>
		public static void BeginFrame()
		{
			_sliderSeq = 0;
		}

		private static float Smoothstep(float t)
		{
			t = Mathf.Clamp01(t);
			return t * t * (3f - 2f * t);
		}

		private static float AnimToward(Dictionary<string, float> dict, string key, float target, float speed = 10f)
		{
			if (!dict.TryGetValue(key, out float v)) v = target;
			if (Event.current.type == EventType.Repaint)
				v = Mathf.MoveTowards(v, target, Time.unscaledDeltaTime * speed);
			dict[key] = v;
			return Smoothstep(v);
		}

		private static float PadS() => WidgetPad * Mathf.Max(0.5f, MainUI.scale);

		/// <summary>
		/// Draw a new-design slider into an absolute rect (track + circle knob).
		/// Used by notifications and any absolute-layout UI.
		/// </summary>
		public static float DrawSliderInRect(Rect row, float value, float min, float max, bool interactive = true, string animKey = null)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.EnsureInit(64, 64);

			int id = animKey != null ? animKey.GetHashCode() : (++_sliderSeq);
			string key = animKey ?? ("hs_" + id);

			var e = Event.current;
			float trackH = 10f * s;
			// Equal inset on left/right and vertical centering
			var track = new Rect(row.x + pad, row.y + (row.height - trackH) * 0.5f, Mathf.Max(1f, row.width - pad * 2f), trackH);
			bool hover = interactive && row.Contains(e.mousePosition);

			float Range01(float mx)
			{
				float inset = 10f * s;
				float usable = Mathf.Max(1f, track.width - 2f * inset);
				return Mathf.Clamp01((mx - (track.x + inset)) / usable);
			}

			if (interactive)
			{
				if (e.type == EventType.MouseDown && e.button == 0 && hover)
				{
					_activeSliderId = id;
					value = Mathf.Lerp(min, max, Range01(e.mousePosition.x));
					if (!_suppressSliderDirty) Bane.config?.MarkDirty();
					e.Use();
				}
				else if (_activeSliderId == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseMove))
				{
					value = Mathf.Lerp(min, max, Range01(e.mousePosition.x));
					if (!_suppressSliderDirty) Bane.config?.MarkDirty();
					e.Use();
				}
				else if (_activeSliderId == id && (e.type == EventType.MouseUp || !UnityEngine.Input.GetMouseButton(0)))
				{
					_activeSliderId = -1;
					if (e.type == EventType.MouseUp) e.Use();
				}
			}

			bool active = interactive && _activeSliderId == id;
			float k = AnimToward(SliderHoverAnims, key, (hover || active) ? 1f : 0f, 10f);
			float tNorm = (max > min) ? Mathf.InverseLerp(min, max, value) : 0f;

			BaneGui.Rounded(track, new Color(0.078f, 0.078f, 0.078f, 1f), Mathf.Max(3, (int)(5f * s)));

			// Filled portion (accent) for progress-style look
			float fillW = track.width * tNorm;
			if (fillW > 1f)
			{
				var fill = new Rect(track.x, track.y, fillW, track.height);
				Color ac = Styles.currentPrimaryColor;
				BaneGui.Rounded(fill, new Color(ac.r, ac.g, ac.b, 0.55f), Mathf.Max(3, (int)(5f * s)));
			}

			float inset = 10f * s;
			float cx = track.x + inset + (track.width - 2f * inset) * tNorm;
			float cy = track.y + track.height * 0.5f;
			float d = (20f + 2f * k) * s;
			BaneGui.Circle(new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d), Styles.currentPrimaryColor);
			return value;
		}

		/// <summary>
		/// Drop-in replacement for GUILayout.HorizontalSlider - equal padding all sides.
		/// </summary>
		public static float HorizontalSlider(float value, float min, float max)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float knob = 22f * s; // max knob size with hover growth
			float rowH = knob + pad * 2f;
			Rect row = GUILayoutUtility.GetRect(10f, rowH, GUILayout.ExpandWidth(true), GUILayout.Height(rowH));
			GUILayout.Space(pad); // equal outer gap below
			return DrawSliderInRect(row, value, min, max, true);
		}

		/// <summary>
		/// Slider that reports the current drag value every frame, but sets
		/// <paramref name="released"/> only on the mouse-up that ends the drag.
		/// Use for settings that should not apply until the user lets go.
		/// </summary>
		/// <summary>True after ForceEndSliderDrag until consumed by HorizontalSliderDeferred.</summary>
		static bool _forceSliderReleased;

		/// <summary>End any active slider drag (e.g. mouse released outside the menu).</summary>
		public static void ForceEndSliderDrag()
		{
			if (_activeSliderId != -1)
			{
				_activeSliderId = -1;
				_forceSliderReleased = true;
			}
			// Also clear if mouse is up but we still think we're dragging
			else if (!UnityEngine.Input.GetMouseButton(0) && _forceSliderReleased == false)
			{
				// no-op
			}
		}

		public static float HorizontalSliderDeferred(float value, float min, float max, out bool released)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float knob = 22f * s;
			float rowH = knob + pad * 2f;
			Rect row = GUILayoutUtility.GetRect(10f, rowH, GUILayout.ExpandWidth(true), GUILayout.Height(rowH));
			GUILayout.Space(pad);

			bool wasDirtyBlocked = _suppressSliderDirty;
			_suppressSliderDirty = true;
			int prevActive = _activeSliderId;
			// Global mouse-up fallback (release outside the menu still ends the drag)
			if (prevActive != -1 && !UnityEngine.Input.GetMouseButton(0) && Event.current.type != EventType.MouseDown)
			{
				_activeSliderId = -1;
				_forceSliderReleased = true;
			}
			float result = DrawSliderInRect(row, value, min, max, true);
			// DrawSliderInRect clears _activeSliderId on MouseUp - treat that as release
			released = (prevActive != -1 && _activeSliderId == -1) || _forceSliderReleased;
			if (released) _forceSliderReleased = false;
			_suppressSliderDirty = wasDirtyBlocked;
			return result;
		}

		static bool _suppressSliderDirty;

		/// <summary>
		/// Labeled slider with equal padding.
		/// </summary>
		public static float Slider(string text, float v, float min, float max)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float labelH = 18f * s;
			float knob = 22f * s;
			float totalH = pad + labelH + pad + knob + pad;
			Rect row = GUILayoutUtility.GetRect(10f, totalH, GUILayout.ExpandWidth(true), GUILayout.Height(totalH));
			GUILayout.Space(pad);

			var lbl = new GUIStyle(GUI.skin.label)
			{
				fontSize = (int)(13f * s),
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = new Color(0.93f, 0.93f, 0.94f, 1f) }
			};
			GUI.Label(new Rect(row.x + pad, row.y + pad, row.width - pad * 2f, labelH), text, lbl);

			var trackRow = new Rect(row.x, row.y + pad + labelH + pad, row.width, knob);
			return DrawSliderInRect(trackRow, v, min, max, true, "ls_" + text);
		}

		/// <summary>
		/// New-design pill toggle with equal padding on every side.
		/// </summary>
		public static bool Toggle(bool state, string label, string stableKey = null,
			[CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLine = 0)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float pillH = 20f * s;
			float pillW = 40f * s;
			float rowH = pillH + pad * 2f;
			Rect row = GUILayoutUtility.GetRect(10f, rowH, GUILayout.ExpandWidth(true), GUILayout.Height(rowH));
			GUILayout.Space(pad);

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.Opacity = Styles.menuOpacity;
			BaneGui.EnsureInit(64, 64);

			// Prefer explicit stableKey (required for toggles in loops); else call-site
			string animKey = !string.IsNullOrEmpty(stableKey)
				? "tog_" + stableKey
				: "tog_" + callerFile + ":" + callerLine;
			float target = state ? 1f : 0f;
			// Snap on first sight so opening a tab does not play a fake transition
			if (!ToggleAnimations.ContainsKey(animKey))
				ToggleAnimations[animKey] = target;
			float t = AnimToward(ToggleAnimations, animKey, target, 8f);

			// Equal pad: pill inset by `pad` from left and vertically centered
			var pill = new Rect(row.x + pad, row.y + pad, pillW, pillH);

			var e = Event.current;
			// Only the pill itself is clickable - not the label / full row
			bool hover = pill.Contains(e.mousePosition);
			if (e.type == EventType.MouseDown && e.button == 0 && hover)
			{
				state = !state;
				Bane.config?.MarkDirty();
				e.Use();
			}

			float hAnim = AnimToward(ToggleAnimations, animKey + "_h", hover ? 1f : 0f, 10f);
			Color offc = Color.Lerp(new Color(0.078f, 0.078f, 0.078f, 1f), new Color(0.135f, 0.135f, 0.135f, 1f), hAnim);
			Color accent = Styles.currentPrimaryColor;
			Color onc = new Color(accent.r * 0.51f, accent.g * 0.36f, accent.b * 0.52f, 1f);
			BaneGui.Rounded(pill, Color.Lerp(offc, onc, t), Mathf.Max(4, (int)(pillH * 0.5f)));

			float kx = Mathf.Lerp(pill.x, pill.xMax - pillH, t);
			BaneGui.Circle(new Rect(kx, pill.y, pillH, pillH), accent);

			// Label starts after pill + same pad gap
			float labelX = pill.xMax + pad;
			var labelRect = new Rect(labelX, row.y, row.xMax - labelX - pad, row.height);
			var lbl = new GUIStyle(GUI.skin.label)
			{
				fontSize = (int)(13f * s),
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = new Color(0.93f, 0.93f, 0.94f, 1f) }
			};
			GUI.Label(labelRect, label, lbl);
			return state;
		}

		/// <summary>
		/// New-design accent pill button with equal padding.
		/// </summary>
		public static bool Button(string label, params GUILayoutOption[] options)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float h = 24f * s + pad * 2f; // text area + equal pad top/bottom baked into height
			GUILayoutOption[] opts;
			if (options != null && options.Length > 0)
			{
				var list = new System.Collections.Generic.List<GUILayoutOption>(options);
				list.Add(GUILayout.MinHeight(h));
				opts = list.ToArray();
			}
			else
			{
				opts = new[] { GUILayout.ExpandWidth(true), GUILayout.Height(h) };
			}
			Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, opts);
			if (r.height < h * 0.8f) r.height = h;
			GUILayout.Space(pad);

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.EnsureInit(64, 64);

			// Shrink visual pill by pad on all sides so neighbouring buttons share equal gaps
			var visual = new Rect(r.x + pad, r.y + pad, Mathf.Max(1f, r.width - pad * 2f), Mathf.Max(1f, r.height - pad * 2f));

			bool hover, pressed;
			bool clicked = BaneGui.Behave(visual, out hover, out pressed);
			string akey = "btn_" + label + "_" + ((int)r.x) + "_" + ((int)r.y);
			float anim = AnimToward(ToggleAnimations, akey, hover ? 1f : 0f, 10f);

			Color baseC = new Color(
				Styles.currentPrimaryColor.r * 0.96f,
				Styles.currentPrimaryColor.g * 0.94f,
				Styles.currentPrimaryColor.b * 0.92f, 1f);
			Color c = Color.Lerp(baseC, Color.Lerp(baseC, Color.white, 0.18f), anim);
			if (pressed) c = new Color(c.r * 0.78f, c.g * 0.78f, c.b * 0.78f, c.a);

			BaneGui.Rounded(visual, c, (int)Mathf.Max(4, visual.height * 0.5f));
			var lbl = new GUIStyle(GUI.skin.label)
			{
				fontSize = (int)(13f * s),
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Normal,
				normal = { textColor = Color.white },
				clipping = TextClipping.Clip
			};
			GUI.Label(visual, label, lbl);
			return clicked;
		}

		/// <summary>Alias for Button (new design).</summary>
		public static bool AccentButton(string label, params GUILayoutOption[] options)
		{
			return Button(label, options);
		}

		/// <summary>
		/// Compact pill toggle with equal outer padding (for tight horizontal rows).
		/// </summary>
		public static bool TogglePill(bool state, string animKey)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float pillH = 20f * s;
			float pillW = 40f * s;
			float boxH = pillH + pad * 2f;
			float boxW = pillW + pad * 2f;
			Rect box = GUILayoutUtility.GetRect(boxW, boxH, GUILayout.Width(boxW), GUILayout.Height(boxH));

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.EnsureInit(64, 64);

			float target = state ? 1f : 0f;
			if (!ToggleAnimations.ContainsKey(animKey))
				ToggleAnimations[animKey] = target;
			float tAnim = AnimToward(ToggleAnimations, animKey, target, 8f);
			var e = Event.current;
			var pillHit = new Rect(box.x + pad, box.y + pad, pillW, pillH);
			bool hover = pillHit.Contains(e.mousePosition);
			if (e.type == EventType.MouseDown && e.button == 0 && hover)
			{
				state = !state;
				Bane.config?.MarkDirty();
				e.Use();
			}

			float hAnim = AnimToward(ToggleAnimations, animKey + "_h", hover ? 1f : 0f, 10f);
			var pill = new Rect(box.x + pad, box.y + pad, pillW, pillH);
			Color offc = Color.Lerp(new Color(0.078f, 0.078f, 0.078f, 1f), new Color(0.135f, 0.135f, 0.135f, 1f), hAnim);
			Color accent = Styles.currentPrimaryColor;
			Color onc = new Color(accent.r * 0.51f, accent.g * 0.36f, accent.b * 0.52f, 1f);
			BaneGui.Rounded(pill, Color.Lerp(offc, onc, tAnim), (int)(pillH * 0.5f));

			float kx = Mathf.Lerp(pill.x, pill.xMax - pillH, tAnim);
			BaneGui.Circle(new Rect(kx, pill.y, pillH, pillH), accent);
			return state;
		}

		/// <summary>
		/// Horizontal sub-tab strip. Returns the selected index.
		/// </summary>
		public static int SubTabs(string[] names, int selected, string stableKey = "subtabs")
		{
			if (names == null || names.Length == 0) return 0;
			selected = Mathf.Clamp(selected, 0, names.Length - 1);

			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float h = 26f * s + pad * 2f;
			Rect row = GUILayoutUtility.GetRect(10f, h, GUILayout.ExpandWidth(true), GUILayout.Height(h));
			GUILayout.Space(pad);

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.EnsureInit(64, 64);

			float gap = 4f * s;
			float innerPad = pad;
			float usableW = row.width - innerPad * 2f;
			float tabW = (usableW - gap * (names.Length - 1)) / names.Length;
			float y = row.y + pad;
			float th = row.height - pad * 2f;

			var e = Event.current;
			for (int i = 0; i < names.Length; i++)
			{
				var r = new Rect(row.x + innerPad + i * (tabW + gap), y, tabW, th);
				bool hover = r.Contains(e.mousePosition);
				bool on = i == selected;
				if (e.type == EventType.MouseDown && e.button == 0 && hover)
				{
					if (selected != i)
					{
						BaneGui.ClosePopup();
						BaneGui.Unfocus();
					}
					selected = i;
					e.Use();
				}

				Color bg;
				if (on)
					bg = Styles.currentPrimaryColor;
				else if (hover)
					bg = new Color(0.18f, 0.18f, 0.18f, 1f);
				else
					bg = new Color(0.12f, 0.12f, 0.12f, 1f);

				BaneGui.Rounded(r, bg, Mathf.Max(4, (int)(th * 0.35f)));
				var lbl = new GUIStyle(GUI.skin.label)
				{
					fontSize = (int)(12f * s),
					alignment = TextAnchor.MiddleCenter,
					fontStyle = on ? FontStyle.Bold : FontStyle.Normal,
					normal = { textColor = on ? Color.white : new Color(0.85f, 0.85f, 0.86f, 1f) },
					clipping = TextClipping.Clip
				};
				GUI.Label(r, names[i], lbl);
			}
			return selected;
		}

		/// <summary>
		/// New-design dropdown via GUILayout. stableKey keeps the open popup across frames.
		/// </summary>
		public static int Dropdown(string label, string[] options, int selected, string stableKey = null)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float h = 24f * s + pad * 2f;
			// Extra layout height when this dropdown is open so the list is not covered by the next row
			string key = stableKey ?? ("dd_" + label);
			int id = (key.GetHashCode() & 0x7fffffff) + 800000;
			bool open = BaneGui.PopupOpen && BaneGui.IsPopupId(id);
			float listExtra = 0f;
			if (open && options != null)
			{
				const float itemH = 26f;
				listExtra = Mathf.Min(options.Length * itemH + 10f, itemH * 7f + 10f);
			}
			Rect row = GUILayoutUtility.GetRect(10f, h + listExtra, GUILayout.ExpandWidth(true), GUILayout.Height(h + listExtra));
			GUILayout.Space(pad);

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.EnsureInit(64, 64);
			BaneGui.BoundsH = 2000f;

			var inner = new Rect(row.x + pad, row.y + pad, Mathf.Max(1f, row.width - pad * 2f), h - pad * 0f);
			inner.height = 24f * s;
			return BaneGui.Dropdown(inner, label, options, selected, key);
		}

		/// <summary>
		/// New-design text box (BaneGui) via GUILayout. Press Enter to commit.
		/// </summary>
		public static string TextBox(string label, string text, string name, out bool committed, int maxLen = 256)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float h = 24f * s + pad * 2f;
			Rect row = GUILayoutUtility.GetRect(10f, h, GUILayout.ExpandWidth(true), GUILayout.Height(h));
			GUILayout.Space(pad);

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.EnsureInit(64, 64);

			var inner = new Rect(row.x + pad, row.y + pad, Mathf.Max(1f, row.width - pad * 2f), Mathf.Max(1f, row.height - pad * 2f));
			string result = BaneGui.TextBox(inner, label, text ?? "", name, out committed, maxLen);
			if (committed || result != text)
				Bane.config?.MarkDirty();
			return result;
		}

		private static bool DrawPillToggleButton(string animKey, bool on)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float pillH = 20f * s;
			float pillW = 40f * s;
			float boxH = pillH + pad * 2f;
			float boxW = pillW + pad * 2f;
			Rect box = GUILayoutUtility.GetRect(boxW, boxH, GUILayout.Width(boxW), GUILayout.Height(boxH));

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.EnsureInit(64, 64);

			float target = on ? 1f : 0f;
			if (!ToggleAnimations.ContainsKey(animKey))
				ToggleAnimations[animKey] = target;
			float t = AnimToward(ToggleAnimations, animKey, target, 8f);
			var e = Event.current;
			var pillHit = new Rect(box.x + pad, box.y + pad, pillW, pillH);
			bool hover = pillHit.Contains(e.mousePosition);
			bool clicked = false;
			if (e.type == EventType.MouseDown && e.button == 0 && hover)
			{
				clicked = true;
				Bane.config?.MarkDirty();
				e.Use();
			}

			float hAnim = AnimToward(ToggleAnimations, animKey + "_h", hover ? 1f : 0f, 10f);
			var pill = new Rect(box.x + pad, box.y + pad, pillW, pillH);
			Color offc = Color.Lerp(new Color(0.078f, 0.078f, 0.078f, 1f), new Color(0.135f, 0.135f, 0.135f, 1f), hAnim);
			Color accent = Styles.currentPrimaryColor;
			Color onc = new Color(accent.r * 0.51f, accent.g * 0.36f, accent.b * 0.52f, 1f);
			BaneGui.Rounded(pill, Color.Lerp(offc, onc, t), (int)(pillH * 0.5f));

			float kx = Mathf.Lerp(pill.x, pill.xMax - pillH, t);
			BaneGui.Circle(new Rect(kx, pill.y, pillH, pillH), accent);
			return clicked;
		}

		private static GUIStyle PillToggleLabelStyle()
		{
			float s = Mathf.Max(0.5f, MainUI.scale);
			float pad = PadS();
			float pillH = 20f * s;
			GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
			labelStyle.fixedHeight = pillH + pad * 2f;
			labelStyle.alignment = TextAnchor.MiddleLeft;
			labelStyle.margin = new RectOffset { left = 0, right = 0, top = 0, bottom = 0 };
			labelStyle.padding = new RectOffset { left = 0, right = 0, top = 0, bottom = 0 };
			labelStyle.fontSize = (int)(13f * s);
			labelStyle.normal.textColor = new Color(0.93f, 0.93f, 0.94f, 1f);
			return labelStyle;
		}
	}
}
