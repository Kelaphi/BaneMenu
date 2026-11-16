using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using UnityEngine;

namespace BaneMenu.ui
{
	// Token: 0x0200000F RID: 15
	public class Styles
	{
		// Token: 0x17000010 RID: 16
		// (get) Token: 0x0600005A RID: 90 RVA: 0x00004FB8 File Offset: 0x000031B8
		public static GUIStyle MainBox
		{
			get
			{
				EnsureStyles();
				return _mainBox;
			}
		}

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x0600005B RID: 91 RVA: 0x000050AC File Offset: 0x000032AC
		public static GUIStyle SectionBox
		{
			get
			{
				EnsureStyles();
				return _sectionBox;
			}
		}

		// Token: 0x17000012 RID: 18
		// (get) Token: 0x0600005C RID: 92 RVA: 0x00005110 File Offset: 0x00003310
		public static GUIStyle SectionBoxActive
		{
			get
			{
				EnsureStyles();
				return _sectionBoxActive;
			}
		}

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x0600005D RID: 93 RVA: 0x00005198 File Offset: 0x00003398
		public static GUIStyle PlayerBox
		{
			get
			{
				EnsureStyles();
				return _playerBox;
			}
		}

		// Token: 0x17000014 RID: 20
		// (get) Token: 0x0600005E RID: 94 RVA: 0x00005200 File Offset: 0x00003400
		public static GUIStyle PlayerBoxActive
		{
			get
			{
				EnsureStyles();
				return _playerBoxActive;
			}
		}

		// Token: 0x0600005F RID: 95 RVA: 0x00005288 File Offset: 0x00003488
		public static GUIStyle CreateCrewmateColorBox(string colorName, Color color)
		{
			GUIStyle guistyle = new GUIStyle();
			Texture2D texture2D = Styles.CreateColoredTexture(colorName, color, 1f);
			guistyle.normal.background = texture2D;
			return guistyle;
		}

		// Token: 0x06000060 RID: 96 RVA: 0x000052B4 File Offset: 0x000034B4
		private static Texture2D CreateColoredTexture(string textureName, Color color, float opacity = 1f)
		{
			Texture2D texture2D;
			if (Styles.CachedTextures.TryGetValue(textureName, out texture2D))
			{
				if (texture2D != null)
					return texture2D;
				Styles.CachedTextures.Remove(textureName);
			}
			texture2D = new Texture2D(1, 1);
			texture2D.SetPixel(0, 0, color.SetAlpha(opacity));
			texture2D.Apply();
			Styles.CachedTextures[textureName] = texture2D;
			return texture2D;
		}

		// Token: 0x06000061 RID: 97 RVA: 0x0000533C File Offset: 0x0000353C
		public static Texture2D GetPillTexture(int width, int height, Color color, string cacheKey)
		{
			Texture2D texture2D;
			if (Styles.CachedTextures.TryGetValue(cacheKey, out texture2D))
			{
				if (texture2D != null)
					return texture2D;
				Styles.CachedTextures.Remove(cacheKey);
			}
			ManualLogSource log = Bane.Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(59, 1, out bool flag);
			if (flag)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Cache lookup for pill texture ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted<string>(cacheKey);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" returned a miss, creating...");
			}
			log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			texture2D = new Texture2D(width, height);
			texture2D.filterMode = FilterMode.Point;
			float num = (float)height / 2f;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					bool flag2 = false;
					float num2 = num;
					if ((float)j < num)
					{
						float num3 = num;
						if (((float)j - num3) * ((float)j - num3) + ((float)i - num2) * ((float)i - num2) <= num * num)
						{
							flag2 = true;
						}
					}
					else if ((float)j > (float)width - num)
					{
						float num4 = (float)width - num;
						if (((float)j - num4) * ((float)j - num4) + ((float)i - num2) * ((float)i - num2) <= num * num)
						{
							flag2 = true;
						}
					}
					else
					{
						flag2 = true;
					}
					if (flag2)
					{
						texture2D.SetPixel(j, i, color);
					}
					else
					{
						texture2D.SetPixel(j, i, Color.clear);
					}
				}
			}
			texture2D.Apply();
			Styles.CachedTextures[cacheKey] = texture2D;
			return texture2D;
		}

		// Token: 0x06000062 RID: 98 RVA: 0x0000547C File Offset: 0x0000367C
		public static Texture2D GetCircleTexture(int size, Color color, string cacheKey)
		{
			Texture2D texture2D;
			if (Styles.CachedTextures.TryGetValue(cacheKey, out texture2D))
			{
				if (texture2D != null)
					return texture2D;
				Styles.CachedTextures.Remove(cacheKey);
			}
			ManualLogSource log = Bane.Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(61, 1, out bool flag);
			if (flag)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Cache lookup for circle texture ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted<string>(cacheKey);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" returned a miss, creating...");
			}
			log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			texture2D = new Texture2D(size, size);
			texture2D.filterMode = FilterMode.Point;
			float num = (float)size / 2f;
			float num2 = num;
			float num3 = num;
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					float num4 = (float)j + 0.5f - num2;
					float num5 = (float)i + 0.5f - num3;
					float num6 = Mathf.Sqrt(num4 * num4 + num5 * num5);
					float num7 = 0.75f;
					if (num6 <= num - num7)
					{
						texture2D.SetPixel(j, i, color);
					}
					else if (num6 <= num + num7)
					{
						float num8 = Mathf.Clamp01((num + num7 - num6) / (2f * num7));
						texture2D.SetPixel(j, i, new Color(color.r, color.g, color.b, color.a * num8));
					}
					else
					{
						texture2D.SetPixel(j, i, Color.clear);
					}
				}
			}
			texture2D.Apply();
			Styles.CachedTextures[cacheKey] = texture2D;
			return texture2D;
		}

		// Token: 0x06000063 RID: 99 RVA: 0x000055D8 File Offset: 0x000037D8
		public static Texture2D GetRoundedRectTexture(int width, int height, float radius, Color color, string cacheKey)
		{
			Texture2D texture2D;
			if (Styles.CachedTextures.TryGetValue(cacheKey, out texture2D))
			{
				if (texture2D != null)
					return texture2D;
				Styles.CachedTextures.Remove(cacheKey);
			}
			ManualLogSource log = Bane.Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(67, 1, out bool flag);
			if (flag)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Cache lookup for rounded rect texture ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted<string>(cacheKey);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" returned a miss, creating...");
			}
			log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			texture2D = new Texture2D(width, height);
			texture2D.filterMode = FilterMode.Point;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					bool flag2 = false;
					if ((float)j + 0.5f < radius && (float)i + 0.5f < radius)
					{
						float num = (float)j + 0.5f - radius;
						float num2 = (float)i + 0.5f - radius;
						if (num * num + num2 * num2 <= radius * radius)
						{
							flag2 = true;
						}
					}
					else if ((float)j + 0.5f < radius && (float)i + 0.5f > (float)height - radius)
					{
						float num3 = (float)height - radius;
						float num4 = (float)j + 0.5f - radius;
						float num5 = (float)i + 0.5f - num3;
						if (num4 * num4 + num5 * num5 <= radius * radius)
						{
							flag2 = true;
						}
					}
					else if ((float)j + 0.5f > (float)width - radius && (float)i + 0.5f < radius)
					{
						float num6 = (float)width - radius;
						float num7 = (float)j + 0.5f - num6;
						float num8 = (float)i + 0.5f - radius;
						if (num7 * num7 + num8 * num8 <= radius * radius)
						{
							flag2 = true;
						}
					}
					else if ((float)j + 0.5f > (float)width - radius && (float)i + 0.5f > (float)height - radius)
					{
						float num9 = (float)width - radius;
						float num10 = (float)height - radius;
						float num11 = (float)j + 0.5f - num9;
						float num12 = (float)i + 0.5f - num10;
						if (num11 * num11 + num12 * num12 <= radius * radius)
						{
							flag2 = true;
						}
					}
					else
					{
						flag2 = true;
					}
					if (flag2)
					{
						texture2D.SetPixel(j, i, color);
					}
					else
					{
						texture2D.SetPixel(j, i, Color.clear);
					}
				}
			}
			texture2D.Apply();
			Styles.CachedTextures[cacheKey] = texture2D;
			return texture2D;
		}

		// Token: 0x06000064 RID: 100 RVA: 0x000057DC File Offset: 0x000039DC
		public static Texture2D GetToggleTexture(int width, int height, int knobSize, float t, Color primaryColor, string cacheKey)
		{
			Texture2D texture2D;
			if (Styles.CachedTextures.TryGetValue(cacheKey, out texture2D))
			{
				if (texture2D != null)
					return texture2D;
				Styles.CachedTextures.Remove(cacheKey);
			}
			ManualLogSource log = Bane.Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(61, 1, out bool flag);
			if (flag)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Cache lookup for toggle texture ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted<string>(cacheKey);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" returned a miss, creating...");
			}
			log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			texture2D = new Texture2D(width, height);
			texture2D.filterMode = FilterMode.Point;
			Color color = new Color(0.08f, 0.08f, 0.08f, 1f);
			Color color2 = new Color(primaryColor.r * 0.5f, primaryColor.g * 0.5f, primaryColor.b * 0.5f, 1f);
			Color color3 = Color.Lerp(color, color2, t);
			float num = (float)(height - knobSize) / 2f;
			float num2 = (float)knobSize / 2f;
			float num3 = num + num2;
			float num4 = (float)width - num - num2;
			float num5 = Mathf.Lerp(num3, num4, t);
			float num6 = (float)height / 2f;
			float num7 = (float)height / 2f;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					bool flag2 = false;
					if ((float)j + 0.5f < num7)
					{
						float num8 = num7;
						float num9 = num7;
						float num10 = (float)j + 0.5f - num8;
						float num11 = (float)i + 0.5f - num9;
						if (Mathf.Sqrt(num10 * num10 + num11 * num11) <= num7)
						{
							flag2 = true;
						}
					}
					else if ((float)j + 0.5f > (float)width - num7)
					{
						float num12 = (float)width - num7;
						float num13 = num7;
						float num14 = (float)j + 0.5f - num12;
						float num15 = (float)i + 0.5f - num13;
						if (Mathf.Sqrt(num14 * num14 + num15 * num15) <= num7)
						{
							flag2 = true;
						}
					}
					else
					{
						flag2 = true;
					}
					Color color4 = Color.clear;
					if (flag2)
					{
						color4 = color3;
					}
					float num16 = (float)j + 0.5f - num5;
					float num17 = (float)i + 0.5f - num6;
					float num18 = Mathf.Sqrt(num16 * num16 + num17 * num17);
					float num19 = 0.75f;
					if (num18 <= num2 - num19)
					{
						color4 = primaryColor;
					}
					else if (num18 <= num2 + num19)
					{
						float num20 = Mathf.Clamp01((num2 + num19 - num18) / (2f * num19));
						color4 = Color.Lerp(color4, primaryColor, num20);
					}
					texture2D.SetPixel(j, i, color4);
				}
			}
			texture2D.Apply();
			Styles.CachedTextures[cacheKey] = texture2D;
			return texture2D;
		}

		// Token: 0x17000015 RID: 21
		// (get) Token: 0x06000065 RID: 101 RVA: 0x00005A44 File Offset: 0x00003C44
		public static GUIStyle CustomButtonStyle
		{
			get
			{
				EnsureStyles();
				return _customButton;
			}
		}

		// Token: 0x06000066 RID: 102 RVA: 0x00005DE4 File Offset: 0x00003FE4
		public static Texture2D GetVerticalPillTexture(int width, int height, Color color, string cacheKey)
		{
			Texture2D texture2D;
			if (Styles.CachedTextures.TryGetValue(cacheKey, out texture2D))
			{
				if (texture2D != null)
					return texture2D;
				Styles.CachedTextures.Remove(cacheKey);
			}
			ManualLogSource log = Bane.Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(68, 1, out bool flag);
			if (flag)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Cache lookup for vertical pill texture ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted<string>(cacheKey);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" returned a miss, creating...");
			}
			log.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			texture2D = new Texture2D(width, height);
			texture2D.filterMode = FilterMode.Point;
			float num = (float)width / 2f;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					bool flag2 = false;
					float num2 = num;
					if ((float)i + 0.5f < num)
					{
						float num3 = num;
						float num4 = (float)j + 0.5f - num2;
						float num5 = (float)i + 0.5f - num3;
						if (num4 * num4 + num5 * num5 <= num * num)
						{
							flag2 = true;
						}
					}
					else if ((float)i + 0.5f > (float)height - num)
					{
						float num6 = (float)height - num;
						float num7 = (float)j + 0.5f - num2;
						float num8 = (float)i + 0.5f - num6;
						if (num7 * num7 + num8 * num8 <= num * num)
						{
							flag2 = true;
						}
					}
					else
					{
						flag2 = true;
					}
					if (flag2)
					{
						texture2D.SetPixel(j, i, color);
					}
					else
					{
						texture2D.SetPixel(j, i, Color.clear);
					}
				}
			}
			texture2D.Apply();
			Styles.CachedTextures[cacheKey] = texture2D;
			return texture2D;
		}

		// Token: 0x17000016 RID: 22
		// (get) Token: 0x06000067 RID: 103 RVA: 0x00005F3C File Offset: 0x0000413C
		public static GUIStyle CustomSliderStyle
		{
			get
			{
				EnsureStyles();
				return _customSlider;
			}
		}

		// Token: 0x17000017 RID: 23
		// (get) Token: 0x06000068 RID: 104 RVA: 0x00006070 File Offset: 0x00004270
		public static GUIStyle CustomSliderThumbStyle
		{
			get
			{
				EnsureStyles();
				return _customThumb;
			}
		}

		// Token: 0x17000018 RID: 24
		// (get) Token: 0x06000069 RID: 105 RVA: 0x00006250 File Offset: 0x00004450
		public static GUIStyle CustomScrollbarStyle
		{
			get
			{
				EnsureStyles();
				return _customScroll;
			}
		}

		// Token: 0x17000019 RID: 25
		// (get) Token: 0x0600006A RID: 106 RVA: 0x00006324 File Offset: 0x00004524
		public static GUIStyle CustomScrollbarThumbStyle
		{
			get
			{
				EnsureStyles();
				return _customScrollThumb;
			}
		}

		// Token: 0x1700001A RID: 26
		// (get) Token: 0x0600006B RID: 107 RVA: 0x00006510 File Offset: 0x00004710
		public static GUIStyle NotificationBox
		{
			get
			{
				GUIStyle guistyle = new GUIStyle();
				int num = (int)NotificationManager.BoxSize.x;
				int num2 = (int)NotificationManager.BoxSize.y;
				float num3 = 8f * MainUI.scale;
				if (num3 < 4f)
				{
					num3 = 4f;
				}
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(18, 3);
				defaultInterpolatedStringHandler.AppendLiteral("NotificationBox_");
				defaultInterpolatedStringHandler.AppendFormatted<int>(num);
				defaultInterpolatedStringHandler.AppendLiteral("_");
				defaultInterpolatedStringHandler.AppendFormatted<int>(num2);
				defaultInterpolatedStringHandler.AppendLiteral("_");
				defaultInterpolatedStringHandler.AppendFormatted<float>(num3);
				string text = defaultInterpolatedStringHandler.ToStringAndClear();
				Texture2D roundedRectTexture = Styles.GetRoundedRectTexture(num, num2, num3, new Color(0.15f, 0.15f, 0.15f), text);
				guistyle.normal.background = roundedRectTexture;
				return guistyle;
			}
		}

		// Token: 0x0600006C RID: 108 RVA: 0x000065D0 File Offset: 0x000047D0
		public static void ClearCache()
		{
			foreach (Texture2D texture2D in Styles.CachedTextures.Values)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
			Styles.CachedTextures.Clear();
			InvalidateStyleCache();
		}

		// Token: 0x04000033 RID: 51
		public static Dictionary<Styles.UIColors, Color> ColorValues = new Dictionary<Styles.UIColors, Color>
		{
			{
				Styles.UIColors.Azure,
				new Color(0f, 0.5f, 1f)
			},
			{
				Styles.UIColors.Cardinal,
				new Color(0.77f, 0.12f, 0.23f)
			},
			{
				Styles.UIColors.Pesto,
				new Color(0.05f, 0.5f, 0.13f)
			},
			{
				Styles.UIColors.Pumpkin,
				new Color(1f, 0.18f, 0.04f)
			},
			{
				Styles.UIColors.Violet,
				new Color(0.5f, 0f, 1f)
			}
		};

		// Token: 0x04000034 RID: 52
		public static float menuOpacity = 1f;

		// Token: 0x04000035 RID: 53
		public static Styles.UIColors primaryColor = Styles.UIColors.Cardinal;

		// Token: 0x04000036 RID: 54
		public static Color currentPrimaryColor = new Color(0.77f, 0.12f, 0.23f, 1f);

		// Token: 0x04000037 RID: 55
		private static Dictionary<string, Texture2D> CachedTextures = new Dictionary<string, Texture2D>();

		// --- Performance: Gemini UI rebuilt GUIStyle every getter call every OnGUI (Layout+Repaint) ---
		private static GUIStyle _mainBox, _sectionBox, _sectionBoxActive, _playerBox, _playerBoxActive;
		private static GUIStyle _customButton, _customSlider, _customThumb, _customScroll, _customScrollThumb;
		private static GUIStyle _headerBar, _sidebarBox, _contentBox;
		private static float _cachedScale = -1f;
		private static ThemeId _cachedTheme;
		private static UIColors _cachedPrimary;
		private static float _cachedOpacity = -1f;
		private static bool _stylesDirty = true;
		private static bool _skinPushed;

		public static void InvalidateStyleCache()
		{
			_stylesDirty = true;
			_mainBox = null;
			_sectionBox = null;
			_sectionBoxActive = null;
			_playerBox = null;
			_playerBoxActive = null;
			_customButton = null;
			_customSlider = null;
			_customThumb = null;
			_customScroll = null;
			_customScrollThumb = null;
			_headerBar = null;
			_sidebarBox = null;
			_contentBox = null;
		}

		static bool TexAlive(Texture2D tex)
		{
			// Unity fake-null: destroyed textures must force rebuild
			return tex != null;
		}

		public static void EnsureStyles()
		{
			bool bgOk = _mainBox != null && TexAlive(_mainBox.normal.background);
			if (!_stylesDirty
				&& bgOk
				&& Mathf.Approximately(_cachedScale, MainUI.scale)
				&& _cachedTheme == currentTheme
				&& _cachedPrimary == primaryColor
				&& Mathf.Approximately(_cachedOpacity, menuOpacity))
				return;

			_cachedScale = MainUI.scale;
			_cachedTheme = currentTheme;
			_cachedPrimary = primaryColor;
			_cachedOpacity = menuOpacity;
			_stylesDirty = false;
			SyncPrimaryFromEnum();

			int w = Mathf.Max(8, (int)MainUI.WindowSize.x);
			int h = Mathf.Max(8, (int)MainUI.WindowSize.y);
			float rad = Mathf.Max(4f, 8f * MainUI.scale);
			string mainKey = $"MainBox_{w}_{h}_{rad:0.##}";
			_mainBox = new GUIStyle
			{
				normal = { background = GetRoundedRectTexture(w, h, rad, new Color(0.15f, 0.15f, 0.15f), mainKey), textColor = Color.white },
				alignment = TextAnchor.UpperCenter,
				fontSize = (int)(13f * MainUI.scale)
			};
			_mainBox.padding.top = 5;

			_sectionBox = new GUIStyle
			{
				normal = { textColor = Color.white },
				alignment = TextAnchor.MiddleLeft,
				fontSize = (int)(14f * MainUI.scale)
			};
			_sectionBox.padding.bottom = 1;
			_sectionBox.padding.left = (int)(8f * MainUI.scale);

			_sectionBoxActive = new GUIStyle(_sectionBox);
			_sectionBoxActive.normal.background = CreateColoredTexture("SectionBoxActive", currentPrimaryColor, 1f);

			_playerBox = new GUIStyle
			{
				normal = { textColor = Color.white },
				alignment = TextAnchor.MiddleLeft,
				clipping = TextClipping.Clip,
				richText = true,
				fontSize = (int)(13f * MainUI.scale)
			};
			_playerBox.padding.left = (int)(10f * MainUI.scale);

			_playerBoxActive = new GUIStyle(_playerBox);
			_playerBoxActive.normal.background = CreateColoredTexture("SectionBoxActive", currentPrimaryColor, 1f);

			// Buttons / sliders - build once per scale/color
			int bh = Mathf.Max(8, (int)(22f * MainUI.scale));
			int bw = bh * 2;
			Color c = currentPrimaryColor;
			// New design: near-full accent base, slightly lighter hover, slightly dimmer normal
			Color cDim = new Color(c.r * 0.96f, c.g * 0.94f, c.b * 0.92f, 1f);
			Color cLit = Color.Lerp(cDim, Color.white, 0.18f);
			string kn = $"BtnN_{c.r:0.##}_{c.g:0.##}_{c.b:0.##}_{bw}_{bh}";
			string ka = $"BtnA_{c.r:0.##}_{c.g:0.##}_{c.b:0.##}_{bw}_{bh}";
			string kh = $"BtnH_{c.r:0.##}_{c.g:0.##}_{c.b:0.##}_{bw}_{bh}";
			_customButton = new GUIStyle(GUI.skin.button)
			{
				fontSize = (int)(13f * MainUI.scale),
				alignment = TextAnchor.MiddleCenter,
				normal = { background = GetPillTexture(bw, bh, cDim, kn), textColor = Color.white },
				active = { background = GetPillTexture(bw, bh, c, ka), textColor = Color.white },
				hover = { background = GetPillTexture(bw, bh, cLit, kh), textColor = Color.white },
				focused = { background = GetPillTexture(bw, bh, cDim, kn), textColor = Color.white }
			};
			_customButton.border = new RectOffset { left = bh / 2, right = bh / 2, top = bh / 2, bottom = bh / 2 };
			_customButton.padding = new RectOffset { left = 8, right = 8, top = 4, bottom = 4 };

			// New design: near-black track + circular accent knob
			int sh = Mathf.Max(4, (int)(10f * MainUI.scale));
			_customSlider = new GUIStyle(GUI.skin.horizontalSlider)
			{
				normal = { background = CreateColoredTexture($"Slider_{sh}", new Color(0.078f, 0.078f, 0.078f), 1f) },
				fixedHeight = sh
			};
			int th = Mathf.Max(10, (int)(20f * MainUI.scale));
			string thumbKey = $"ThumbCircle_{th}_{currentPrimaryColor.r:0.##}_{currentPrimaryColor.g:0.##}_{currentPrimaryColor.b:0.##}";
			_customThumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
			{
				normal = { background = GetCircleTexture(th, currentPrimaryColor, thumbKey) },
				fixedWidth = th,
				fixedHeight = th
			};

			// Slim rounded scrollbar (was too wide / looked "fat")
			int sw = Mathf.Max(4, (int)(6f * MainUI.scale));
			float scrollRad = sw * 0.5f;
			// Tall texture so rounded ends stay circular when Unity stretches track/thumb
			int scrollTexH = Mathf.Max(sw * 6, 64);
			_customScroll = new GUIStyle(GUI.skin.verticalScrollbar)
			{
				fixedWidth = sw,
				margin = new RectOffset { left = 2, right = 2, top = 2, bottom = 2 },
				padding = new RectOffset { left = 0, right = 0, top = 0, bottom = 0 },
				normal = { background = GetRoundedRectTexture(sw, scrollTexH, scrollRad, new Color(0.12f, 0.12f, 0.12f, 0.85f), $"ScrR_{sw}_{scrollTexH}") }
			};
			_customScrollThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb)
			{
				fixedWidth = sw,
				margin = new RectOffset { left = 0, right = 0, top = 0, bottom = 0 },
				padding = new RectOffset { left = 0, right = 0, top = 0, bottom = 0 },
				normal = { background = GetRoundedRectTexture(sw, scrollTexH, scrollRad, currentPrimaryColor, $"ScrTR_{sw}_{scrollTexH}_{currentPrimaryColor}") }
			};

			_headerBar = new GUIStyle(GUI.skin.label)
			{
				normal = { textColor = currentPrimaryColor },
				fontSize = (int)(13f * MainUI.scale),
				alignment = TextAnchor.MiddleLeft
			};
			_headerBar.padding = new RectOffset { left = (int)(10f * MainUI.scale), right = 0, top = 0, bottom = 0 };

			_sidebarBox = new GUIStyle { normal = { background = CreateColoredTexture("Sidebar", new Color(0.12f, 0.12f, 0.12f), menuOpacity) } };
			_contentBox = new GUIStyle { normal = { background = CreateColoredTexture("Content", new Color(0.13f, 0.13f, 0.13f), menuOpacity) } };
		}


		// Token: 0x0200004A RID: 74
		public enum UIColors
		{
			// Token: 0x040000BA RID: 186
			Azure,
			// Token: 0x040000BC RID: 188
			Cardinal,
			// Token: 0x040000BD RID: 189
			Pesto,
			// Token: 0x040000BE RID: 190
			Pumpkin,
			// Token: 0x040000C0 RID: 192
			Violet
		}
	
		// --- Hydra compatibility (theme API + chrome aliases) ---
		public enum ThemeId
		{
			Client = 0,
			Classic = 1,
			Rose = 2,
			Roblox = 3,
		}

		public static readonly string[] ThemeNames =
		{
			"Bane (Cardinal)",
			"Classic Bane",
			"Rose",
			"Roblox Menu"
		};

		public static ThemeId currentTheme = ThemeId.Classic;

		public static readonly Color RobloxPurple = new Color(0.55f, 0.15f, 0.85f);
		public static readonly Color RobloxPurpleDark = new Color(0.40f, 0.08f, 0.70f);
		public static readonly Color RobloxRed = new Color(0.55f, 0.05f, 0.08f);
		public static readonly Color RobloxRedHover = new Color(0.70f, 0.10f, 0.12f);
		public static readonly Color RobloxYellow = new Color(1f, 0.92f, 0.15f);
		public static readonly Color RobloxGreen = new Color(0.2f, 0.85f, 0.25f);

		public static bool UseClientChrome => false;
		public static bool UseRobloxLayout => currentTheme == ThemeId.Roblox;

		public static void SetTheme(ThemeId id)
		{
			if (currentTheme == id) return;
			currentTheme = id;
			ClearCache();
			InvalidateStyleCache();
			if (id == ThemeId.Rose)
			{
				primaryColor = UIColors.Cardinal;
				currentPrimaryColor = new Color(0.85f, 0.20f, 0.45f, 1f);
			}
			else
			{
				primaryColor = UIColors.Cardinal;
				currentPrimaryColor = ColorValues[UIColors.Cardinal];
			}
		}

		public static void SyncPrimaryFromEnum()
		{
			if (ColorValues.TryGetValue(primaryColor, out Color c))
				currentPrimaryColor = c;
		}

		public static GUIStyle HeaderBar
		{
			get
			{
				EnsureStyles();
				return _headerBar;
			}
		}

		public static GUIStyle SidebarBox
		{
			get
			{
				EnsureStyles();
				return _sidebarBox;
			}
		}

		public static GUIStyle ContentBox
		{
			get
			{
				EnsureStyles();
				return _contentBox;
			}
		}

		private static GUIStyle _savedButton, _savedSlider, _savedThumb, _savedScroll, _savedScrollThumb;
		private static bool _skinApplied;

		public static void ApplyClientSkin()
		{
			EnsureStyles();
			if (!_skinApplied)
			{
				_savedButton = GUI.skin.button;
				_savedSlider = GUI.skin.horizontalSlider;
				_savedThumb = GUI.skin.horizontalSliderThumb;
				_savedScroll = GUI.skin.verticalScrollbar;
				_savedScrollThumb = GUI.skin.verticalScrollbarThumb;
				_skinApplied = true;
			}
			// Reassign only cached styles (no new GUIStyle allocs)
			GUI.skin.button = _customButton;
			GUI.skin.horizontalSlider = _customSlider;
			GUI.skin.horizontalSliderThumb = _customThumb;
			GUI.skin.verticalScrollbar = _customScroll;
			GUI.skin.verticalScrollbarThumb = _customScrollThumb;
			if (GUI.skin.label != null)
				GUI.skin.label.fontSize = (int)(13f * MainUI.scale);
		}

		public static void ApplyRobloxSkin()
		{
			ApplyClientSkin();
		}

		public static void RestoreSkin()
		{
			if (!_skinApplied) return;
			GUI.skin.button = _savedButton;
			GUI.skin.horizontalSlider = _savedSlider;
			GUI.skin.horizontalSliderThumb = _savedThumb;
			GUI.skin.verticalScrollbar = _savedScroll;
			GUI.skin.verticalScrollbarThumb = _savedScrollThumb;
		}

	}
}
