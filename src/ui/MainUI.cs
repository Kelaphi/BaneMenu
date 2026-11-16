using BaneMenu.ui.sections;
using System;
using InnerNet;
using UnityEngine;

namespace BaneMenu.ui
{
	public class MainUI : MonoBehaviour
	{
		// Current window
		public KeyCode menuKey = KeyCode.Delete;
		public bool visible = false;
		public static float scale = 1.0f;

		private bool isDragging = false;
		private Vector2 mouseDelta = new Vector2();

		public static Vector2 windowPosition = new Vector2(250, 100);
		/// <summary>Inset so content does not square-off the rounded window corners.</summary>
		private static float CornerPad => 10f * scale;

		public static Vector2 WindowSize
		{
			get { return new Vector2(680f, 470f) * scale; }
		}

		// UI Header
		public static Vector2 HeaderSize
		{
			get { return new Vector2(WindowSize.x, 30f * scale); }
		}

		public static Vector2 HeaderPosition
		{
			get { return new Vector2(windowPosition.x, windowPosition.y); }
		}

		// UI Section Pane
		private readonly Section[] sections = { new GeneralSection(), new GameSection(), new SelfSection(), new HostSection(), new PlayersSection(), new FabricatorSection(), new SchizoSection(), new ProtectionsSection(), new DebugSection(), new MenuSection() };
		public byte activeTab = 0;

		/// <summary>Fabricator kept in code but temporarily hidden from the tab bar.
		/// Host tab is only shown when you are effectively host.
		/// Players is only shown when inside a game (lobby or match).
		/// Schizo is only shown after the game has started.</summary>
		private static bool IsSectionVisible(Section section)
		{
			if (section == null) return false;
			if (section.name == "Fabricator") return false;
			if (section.name == "Host" && !global::BaneMenu.Utilities.IsHostEffective()) return false;

			var client = AmongUsClient.Instance;
			bool inGame = client != null && client.GameState != InnerNetClient.GameStates.NotJoined;
			bool freePlay = client != null && client.NetworkMode == NetworkModes.FreePlay;
			bool gameStarted = client != null && (
				client.GameState == InnerNetClient.GameStates.Started
				|| freePlay
				|| ShipStatus.Instance != null);

			if (section.name == "Players" && !inGame) return false;
			if (section.name == "Schizo" && !gameStarted) return false;
			return true;
		}

		private void EnsureActiveTabVisible()
		{
			if(activeTab < sections.Length && IsSectionVisible(sections[activeTab])) return;
			for(byte i = 0; i < sections.Length; i++)
			{
				if(IsSectionVisible(sections[i])) { activeTab = i; return; }
			}
			activeTab = 0;
		}

		/// <summary>April Fools: BonerMenu on April 1st, otherwise BaneMenu.</summary>
		public static string DisplayMenuName
		{
			get
			{
				var now = System.DateTime.Now;
				return (now.Month == 4 && now.Day == 1) ? "BonerMenu" : "BaneMenu";
			}
		}

		private Vector2 sidebarScroll = Vector2.zero;
		private float tabHiY = -1f;

		// Open/close animation (Client theme primarily)
		private float menuAnim = 0f;           // 0 closed → 1 open
		private const float MenuAnimSpeed = 7.5f; // ~0.13s feel
		private bool wasVisible = false;
		private static Texture2D _dimTex;
		private int lastToggleFrame = -1;
		private bool menuKeyHeld = false;

		public static Vector2 SectionListSize
		{
			get { return new Vector2(100f * scale, WindowSize.y - HeaderSize.y - CornerPad); }
		}

		public static Vector2 SectionListPosition
		{
			get { return new Vector2(windowPosition.x, windowPosition.y + HeaderSize.y); }
		}

		public static Vector2 SectionButtonSize
		{
			get { return new Vector2(SectionListSize.x, 25f * scale); }
		}

		// Feature Pane
		public static Vector2 FeaturePaneSize
		{
			get { return new Vector2(WindowSize.x - SectionListSize.x - CornerPad, WindowSize.y - HeaderSize.y - CornerPad); }
		}

		public static Vector2 FeaturePanePosition
		{
			get { return new Vector2(SectionListPosition.x + SectionListSize.x, HeaderPosition.y + HeaderSize.y); }
		}

		public void Update()
		{
			// Edge-detect only in Update - OnGUI KeyDown + GetKeyDown double-fired and canceled single presses
			bool held = Input.GetKey(menuKey);
			if (held && !menuKeyHeld)
			{
				visible = !visible;
				if (!visible)
				{
					BaneGui.ClosePopup();
					BaneGui.Unfocus();
				}
			}
			menuKeyHeld = held;

			if (Input.GetKeyDown(KeyCode.F6))
			{
				System.Random random = new System.Random();
				Bane.notifications.Send("Test", $"The quick brown fox jumps over the lazy dog. {random.Next(0, 100)}");
			}

			// BaneGui text box fallback when Event.character is stripped
			if (visible && BaneGui.TextFocused && BaneGui.CharApiBroken)
			{
				try { BaneGui.FeedInput(Input.inputString); } catch { }
			}

			// Fade in / out toward target
			float target = visible ? 1f : 0f;
			float dt = Time.unscaledDeltaTime;
			if (dt <= 0f) dt = 0.016f;
			menuAnim = Mathf.MoveTowards(menuAnim, target, dt * MenuAnimSpeed);

			if (menuAnim <= 0.001f && !visible) return;

			// Handle changing the current section through arrow keys
			if(Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
			{
				int offset = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;

				activeTab = (byte)Math.Clamp(activeTab + offset, 0, sections.Length - 1);
				tabHiY = activeTab * SectionButtonSize.y; // snap highlight
				BaneGui.ClosePopup();
				BaneGui.Unfocus();
			}

			if(Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown))
			{
				int offset = Input.GetKeyDown(KeyCode.PageUp) ? -1 : 1;

				sections[activeTab].HandleSubsectionMove(offset);
			}

		}

		public void OnGUI()
		{
			if(menuAnim <= 0.001f) return;

			// Push global alpha for this frame's menu chrome
			Color prev = GUI.color;
			float a = SmoothAlpha(menuAnim);
			GUI.color = new Color(1f, 1f, 1f, a);

			if (Styles.UseRobloxLayout)
			{
				DrawRobloxLayout();
				GUI.color = prev;
				return;
			}

			Styles.ApplyClientSkin();
			Controls.BeginFrame();
			BaneGui.BeginFrame(activeTab * 1000);

			// Clicks / mouse-up outside the menu window: end drag, unfocus text, close dropdowns
			var menuWinEarly = new Rect(windowPosition.x, windowPosition.y, WindowSize.x, WindowSize.y);
			var evEarly = Event.current;
			if (evEarly != null)
			{
				bool outside = !menuWinEarly.Contains(evEarly.mousePosition);
				if (outside && evEarly.type == EventType.MouseDown && evEarly.button == 0)
				{
					BaneGui.Unfocus();
					BaneGui.ClosePopup();
					Controls.ForceEndSliderDrag();
				}
				if (!UnityEngine.Input.GetMouseButton(0))
					Controls.ForceEndSliderDrag();
			}

			// Title-bar drag (new design) - before drawing so position is settled for this event
			HandleTitleBarDrag();

			// Sync new-design toolkit with current theme colors
			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.Opacity = a; // opacity setting removed; full opacity when open
			BaneGui.EnsureInit((int)WindowSize.x, (int)WindowSize.y);

			var win = new Rect(windowPosition.x, windowPosition.y, WindowSize.x, WindowSize.y);

			// Soft drop shadow + flat rounded window (new design)
			BaneGui.Shadow(win);
			BaneGui.DrawWindowBg(win);

			// Accent-colored title (drag handle area) - no solid header bar
			var titleStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = (int)(13f * scale),
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = Styles.currentPrimaryColor }
			};
			GUI.Label(new Rect(windowPosition.x + 10f * scale, windowPosition.y + 5f * scale, WindowSize.x - 20f * scale, 20f * scale),
				$"{DisplayMenuName} v{MyPluginInfo.PLUGIN_VERSION}", titleStyle);

			// Section tabs - new design: selected = solid accent bar flush left, hover = subtle wash
			float sideW = SectionListSize.x;
			float rowH = SectionButtonSize.y;
			// tabHiY is LOCAL to the sidebar (0 = first tab), so dragging the window
			// does not make the highlight glide as if tabs were switching.
			int activeVisual = 0;
			for (int vi = 0; vi < activeTab && vi < sections.Length; vi++)
				if (IsSectionVisible(sections[vi])) activeVisual++;
			float targetLocalY = activeVisual * rowH;
			if (tabHiY < 0f) tabHiY = targetLocalY;
			if (Event.current.type == EventType.Repaint)
				tabHiY = Mathf.Lerp(tabHiY, targetLocalY, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));

			// Animated accent highlight for active tab
			BaneGui.Rounded(new Rect(SectionListPosition.x, SectionListPosition.y + tabHiY, sideW, rowH), Styles.currentPrimaryColor, 3);

			EnsureActiveTabVisible();
			int visibleTabCount = 0;
			for (int vi = 0; vi < sections.Length; vi++)
				if (IsSectionVisible(sections[vi])) visibleTabCount++;
			float tabAreaH = SectionListSize.y;
			float totalTabsH = visibleTabCount * rowH;
			if (totalTabsH > tabAreaH)
			{
				GUILayout.BeginArea(new Rect(SectionListPosition.x, SectionListPosition.y, SectionListSize.x, SectionListSize.y));
				sidebarScroll = GUILayout.BeginScrollView(sidebarScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
				for (byte i = 0; i < sections.Length; i++)
				{
					Section section = sections[i];
					if (!IsSectionVisible(section)) continue;
					Rect tr = GUILayoutUtility.GetRect(sideW, rowH, GUILayout.Width(sideW), GUILayout.Height(rowH));
					bool hov = tr.Contains(Event.current.mousePosition);
					if (hov && i != activeTab)
						BaneGui.Rounded(tr, new Color(1f, 1f, 1f, 0.06f), 3);
					var tabLbl = new GUIStyle(GUI.skin.label)
					{
						fontSize = (int)(14f * scale),
						alignment = TextAnchor.MiddleLeft,
						normal = { textColor = new Color(0.95f, 0.95f, 0.96f, 1f) }
					};
					GUI.Label(new Rect(tr.x + 8f * scale, tr.y, tr.width - 8f * scale, tr.height), section.name, tabLbl);
					if (!isDragging && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hov)
					{
						if (activeTab != i)
						{
							BaneGui.ClosePopup();
							BaneGui.Unfocus();
						}
						activeTab = i;
						int snapVis = 0; for (int sv = 0; sv < i; sv++) if (IsSectionVisible(sections[sv])) snapVis++; tabHiY = snapVis * rowH; // snap highlight when switching
						Event.current.Use();
					}
				}
				GUILayout.EndScrollView();
				GUILayout.EndArea();
			}
			else
			{
				int visualRow = 0;
				for (byte i = 0; i < sections.Length; i++)
				{
					Section section = sections[i];
					if (!IsSectionVisible(section)) continue;
					Rect tr = new Rect(
						SectionListPosition.x,
						SectionListPosition.y + visualRow * rowH,
						sideW,
						rowH);
					visualRow++;
					bool hov = tr.Contains(Event.current.mousePosition);
					if (hov && i != activeTab)
						BaneGui.Rounded(tr, new Color(1f, 1f, 1f, 0.06f), 3);
					var tabLbl = new GUIStyle(GUI.skin.label)
					{
						fontSize = (int)(14f * scale),
						alignment = TextAnchor.MiddleLeft,
						normal = { textColor = new Color(0.95f, 0.95f, 0.96f, 1f) }
					};
					GUI.Label(new Rect(tr.x + 8f * scale, tr.y, tr.width - 8f * scale, tr.height), section.name, tabLbl);
					if (!isDragging && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hov)
					{
						if (activeTab != i)
						{
							BaneGui.ClosePopup();
							BaneGui.Unfocus();
						}
						activeTab = i;
						int snapVis = 0; for (int sv = 0; sv < i; sv++) if (IsSectionVisible(sections[sv])) snapVis++; tabHiY = snapVis * rowH; // snap highlight when switching
						Event.current.Use();
					}
				}
			}

			{
				// Consume dropdown wheel/clicks before the content ScrollView steals them
				BaneGui.HandlePopupInput();

				// Feature pane - content drawn on window, no outer chrome
				Section section = sections[activeTab];
				// Freeze content scroll while dropdown is open so the list can scroll
				Vector2 sv = section.scrollVector;
				GUILayout.BeginArea(new Rect(
					FeaturePanePosition.x,
					FeaturePanePosition.y,
					FeaturePaneSize.x,
					FeaturePaneSize.y));
				var prevSv = GUI.skin.scrollView;
				var clearSv = new GUIStyle(GUI.skin.scrollView);
				clearSv.normal.background = null;
				clearSv.hover.background = null;
				clearSv.active.background = null;
				GUI.skin.scrollView = clearSv;
				section.scrollVector = GUILayout.BeginScrollView(section.scrollVector);
				section.Render();
				// Extra bottom space so the last control isn't clipped halfway by the scrollbar edge
				GUILayout.Space(28f * scale);
				GUILayout.EndScrollView();
				GUI.skin.scrollView = prevSv;
				GUILayout.EndArea();
				if (BaneGui.PopupOpen)
					section.scrollVector = sv; // undo any scroll the view applied
			}

			// Dropdowns must draw outside scroll clipping (screen-space header)
			BaneGui.BoundsH = Screen.height;
			BaneGui.DrawPopup();
			BaneGui.DismissPopupIfOutside();
			BaneGui.EndFrame();

			GUI.color = prev;
		}

		private static float SmoothAlpha(float t)
		{
			// smoothstep for a clean ease
			t = Mathf.Clamp01(t);
			return t * t * (3f - 2f * t);
		}

		private void DrawBackdrop(float t)
		{
			// GUI.DrawTexture is stripped on IL2CPP - use GUI.Box + style.background instead
			float a = SmoothAlpha(t) * 0.55f;
			if (a < 0.01f) return;

			Color prev = GUI.color;
			Rect full = new Rect(0, 0, Screen.width, Screen.height);

			GUIStyle veil = new GUIStyle();
			veil.normal.background = DimTex();

			GUI.color = new Color(0.04f, 0.02f, 0.08f, a);
			GUI.Box(full, "", veil);

			GUI.color = new Color(0.35f, 0.20f, 0.55f, a * 0.25f);
			GUI.Box(full, "", veil);

			GUI.color = prev;
		}

		private static Texture2D DimTex()
		{
			if (_dimTex != null) return _dimTex;
			_dimTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			_dimTex.SetPixel(0, 0, Color.white);
			_dimTex.Apply();
			_dimTex.wrapMode = TextureWrapMode.Clamp;
			return _dimTex;
		}

		/// <summary>
		/// Mobile Roblox-cheat style: purple header, red feature rows, yellow text, purple footer.
		/// Single column - categories as red bars, then section content.
		/// </summary>
		private void DrawRobloxLayout()
		{
			Styles.ApplyRobloxSkin();
			Controls.BeginFrame();
			BaneGui.BeginFrame(activeTab * 1000);

			// Larger mobile panel - nested scrolls were fighting each other
			float w = 400f * scale;
			float h = 580f * scale;
			float headerH = 48f * scale;
			float footerH = 48f * scale;
			float catH = 150f * scale; // fixed category strip (own scroll)
			float pad = 8f * scale;

			Rect shell = new Rect(windowPosition.x, windowPosition.y, w, h);
			GUI.Box(shell, "", Styles.MainBox);

			var headerStyle = new GUIStyle();
			headerStyle.normal.background = StylesSolidRobloxHeader();
			headerStyle.normal.textColor = Styles.RobloxYellow;
			headerStyle.alignment = TextAnchor.MiddleCenter;
			headerStyle.fontSize = (int)(16 * scale);
			headerStyle.fontStyle = FontStyle.Bold;
			GUI.Box(new Rect(shell.x, shell.y, w, headerH),
				$"{DisplayMenuName} v{MyPluginInfo.PLUGIN_VERSION}", headerStyle);

			// --- Categories: fixed height, independent scroll ---
			float catY = shell.y + headerH;
			GUILayout.BeginArea(new Rect(shell.x + pad, catY, w - pad * 2f, catH));
			sidebarScroll = GUILayout.BeginScrollView(sidebarScroll, false, true);
			EnsureActiveTabVisible();
			for (byte i = 0; i < sections.Length; i++)
			{
				if (!IsSectionVisible(sections[i])) continue;
				string mark = activeTab == i ? "▶ " : "";
				if (Controls.Button($"{mark}{sections[i].name.ToUpper()}", GUILayout.Height(30f * scale)))
					activeTab = i;
				GUILayout.Space(2f * scale);
			}
			GUILayout.EndScrollView();
			GUILayout.EndArea();

			// Divider label
			float divY = catY + catH;
			var divStyle = new GUIStyle(GUI.skin.label);
			divStyle.normal.textColor = Styles.RobloxYellow;
			divStyle.alignment = TextAnchor.MiddleCenter;
			divStyle.fontStyle = FontStyle.Bold;
			divStyle.fontSize = (int)(12 * scale);
			GUI.Label(new Rect(shell.x, divY, w, 22f * scale),
				$" - - {sections[activeTab].name.ToUpper()} - - ", divStyle);

			// --- Features: one scroll, takes the rest ---
			float bodyY = divY + 22f * scale;
			float bodyH = (shell.y + h - footerH) - bodyY - pad;
			if (bodyH < 80f * scale) bodyH = 80f * scale;

			GUILayout.BeginArea(new Rect(shell.x + pad, bodyY, w - pad * 2f, bodyH));
			sections[activeTab].scrollVector = GUILayout.BeginScrollView(
				sections[activeTab].scrollVector, false, true);
			sections[activeTab].Render();
			GUILayout.EndScrollView();
			GUILayout.EndArea();

			// Footer
			var foot = new GUIStyle();
			foot.normal.background = StylesSolidRobloxHeader();
			GUI.Box(new Rect(shell.x, shell.y + h - footerH, w, footerH), "", foot);

			GUILayout.BeginArea(new Rect(shell.x + pad, shell.y + h - footerH + 6f * scale, w - pad * 2f, footerH - 10f * scale));
			GUILayout.BeginHorizontal();
			if (Controls.Button("CLOSE MENU", GUILayout.Height(32f * scale)))
				visible = false;
			if (Controls.Button("CLASSIC UI", GUILayout.Height(32f * scale)))
				Styles.SetTheme(Styles.ThemeId.Client);
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
		}

		// Tiny helpers so we don't need public Solid - use HeaderBar path via reflection-free duplicates
		private static Texture2D _rbxHdr;
		private Texture2D StylesSolidRobloxHeader()
		{
			if (_rbxHdr != null) return _rbxHdr;
			_rbxHdr = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			_rbxHdr.SetPixel(0, 0, Styles.RobloxPurple);
			_rbxHdr.Apply();
			return _rbxHdr;
		}

		/// <summary>
		/// Title-bar drag handling matching BaneMenuNewDesign (MenuCore):
		/// only the header strip starts a drag; clamp to screen; consume events.
		/// Must be called from OnGUI (Event.current is invalid in Update).
		/// </summary>
		private void HandleTitleBarDrag()
		{
			Event e = Event.current;
			if (e == null) return;

			float titleH = HeaderSize.y;
			Rect title = new Rect(windowPosition.x, windowPosition.y, WindowSize.x, titleH);

			if (e.type == EventType.MouseDown && e.button == 0 && title.Contains(e.mousePosition))
			{
				// Don't start a drag if a widget already captured the click
				if (BaneGui.Dragging) return;
				isDragging = true;
				mouseDelta = e.mousePosition - windowPosition;
				e.Use();
			}
			else if (e.type == EventType.MouseDrag && isDragging)
			{
				windowPosition = e.mousePosition - mouseDelta;
				e.Use();
			}
			else if (e.type == EventType.MouseUp && isDragging)
			{
				isDragging = false;
				e.Use();
			}

			// Clamp to screen (GUI coords, top-left origin)
			float ww = WindowSize.x;
			float hh = WindowSize.y;
			windowPosition.x = Mathf.Clamp(windowPosition.x, 0f, Mathf.Max(0f, Screen.width - ww));
			windowPosition.y = Mathf.Clamp(windowPosition.y, 0f, Mathf.Max(0f, Screen.height - hh));
		}

		private bool IsInBox(Vector2 mousePos)
		{
			float ww = Styles.UseRobloxLayout ? 400f * scale : WindowSize.x;
			float hh = Styles.UseRobloxLayout ? 580f * scale : WindowSize.y;
			return
				mousePos.x >= windowPosition.x &&
				mousePos.x <= (windowPosition.x + ww) &&
				mousePos.y >= windowPosition.y &&
				mousePos.y <= (windowPosition.y + hh);
		}

		/// <summary>
		/// True while the cursor is over the menu (or a drag that started on it is running).
		/// Used by ClickBlocker to stop the game from receiving hover/clicks through the menu.
		/// </summary>
		public static bool MouseBlocked()
		{
			var ui = Bane.mainUI;
			if (ui == null || !ui.visible) return false;
			if (ui.isDragging || BaneGui.Dragging) return true;

			// While a dropdown is open, always block the game so clicks on the list
			// (which may hang below the menu) never reach the world / HUD.
			if (BaneGui.PopupOpen) return true;

			if (BaneGui.IsMouseOverPopup()) return true;

			// Called from game Update patches (not OnGUI) - convert screen → GUI coords
			float gx = Input.mousePosition.x;
			float gy = Screen.height - Input.mousePosition.y;

			float ww = Styles.UseRobloxLayout ? 400f * scale : WindowSize.x;
			float hh = Styles.UseRobloxLayout ? 580f * scale : WindowSize.y;
			return gx >= windowPosition.x - 4f &&
			       gx <= windowPosition.x + ww + 4f &&
			       gy >= windowPosition.y - 4f &&
			       gy <= windowPosition.y + hh + 4f;
		}

		private void RenderTab(byte position, Section section)
		{
			if (!IsSectionVisible(section)) return;
			float gap = 4f * scale;
			float sidePad = 8f * scale;
			float topPad = 8f * scale;
			float h = SectionButtonSize.y;
			Rect rect = new Rect(
				SectionListPosition.x + sidePad,
				SectionListPosition.y + topPad + position * (h + gap),
				SectionListSize.x - sidePad * 2f,
				h
			);

			GUIStyle style = activeTab == position ? Styles.SectionBoxActive : Styles.SectionBox;
			if(GUI.Button(rect, section.name, style))
			{
				activeTab = position;
			}
		}

		public class MainUIConfig
		{
			public KeyCode MenuKey { get; set; }
			public Styles.UIColors PrimaryColor { get; set; }
			public float MenuOpacity { get; set; }
			public float UiScale { get; set; }
			public bool DisableNotifications { get; set; }
			public int Theme { get; set; }
		}

		public MainUIConfig GetConfigData()
		{
			return new MainUIConfig
			{
				MenuKey = menuKey,
				PrimaryColor = Styles.primaryColor,
				MenuOpacity = Styles.menuOpacity,
				UiScale = scale,
				DisableNotifications = Bane.notifications.disableNotifications,
				Theme = (int)Styles.currentTheme
			};
		}

		public void LoadConfigData(MainUIConfig configData)
		{
			if(configData == null) return;

			if(configData.MenuKey != KeyCode.None)
			{
				Bane.mainUI.menuKey = configData.MenuKey;
			}

			Styles.primaryColor = (Styles.UIColors)Math.Clamp((int)configData.PrimaryColor, 0, Styles.ColorValues.Count - 1);
			Styles.SyncPrimaryFromEnum();
			Styles.menuOpacity = 1f; // opacity setting removed
			scale = Mathf.Clamp(configData.UiScale, 0.5f, 2.0f);
			Bane.notifications.disableNotifications = configData.DisableNotifications;
			// Theme UI removed - always use Classic chrome
			Styles.SetTheme(Styles.ThemeId.Classic);
		}
	}
}