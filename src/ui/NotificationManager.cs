using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.ui
{
	internal class NotificationManager : MonoBehaviour
	{
		private class Entry
		{
			public Notification notification;
			public float spawnTime;
			public float animY;      // current animated bottom-offset index space
			public float targetSlot; // integer slot from bottom (0 = lowest)
		}

		private readonly List<Entry> entries = new List<Entry>();
		public bool disableNotifications = false;

		const float AnimSpeed = 12f;
		const float SlideInDuration = 0.28f;

		public static Vector2 BoxSize
		{
			get { return new Vector2(320f, 88f) * MainUI.scale; }
		}

		public static Vector2 BoxHeaderSize
		{
			get { return new Vector2(BoxSize.x, 22f * MainUI.scale); }
		}

		public static float BoxPad
		{
			get { return 8f * MainUI.scale; }
		}

		public static Vector2 BoxSliderSize
		{
			get { return new Vector2(BoxSize.x, 22f * MainUI.scale); }
		}

		public void Update()
		{
			float dt = Time.unscaledDeltaTime;
			if (dt <= 0f) dt = 0.016f;

			for (int i = 0; i < entries.Count; i++)
			{
				Entry e = entries[i];
				e.notification.lifetime += Time.deltaTime;
				e.targetSlot = i; // 0 = bottom-most

				// Smooth move toward slot
				e.animY = Mathf.Lerp(e.animY, e.targetSlot, 1f - Mathf.Exp(-AnimSpeed * dt));

				if (e.notification.HasExpired)
				{
					entries.RemoveAt(i);
					i--;
				}
			}

			Bane.config?.TickAutosave();
		}

		public void OnGUI()
		{
			if (disableNotifications) return;
			if (entries.Count == 0) return;

			BaneGui.Accent = Styles.currentPrimaryColor;
			BaneGui.Opacity = 1f;
			BaneGui.EnsureInit((int)BoxSize.x, (int)BoxSize.y);

			float s = Mathf.Max(0.5f, MainUI.scale);
			float gap = 6f * s;
			float margin = 10f * s;
			float h = BoxSize.y;
			float w = BoxSize.x;

			for (int i = 0; i < entries.Count; i++)
			{
				Entry e = entries[i];
				float age = Time.realtimeSinceStartup - e.spawnTime;

				// Slide in from below + fade in
				float slideT = Mathf.Clamp01(age / SlideInDuration);
				slideT = slideT * slideT * (3f - 2f * slideT); // smoothstep
				float slideOffset = (1f - slideT) * (h + gap + 20f * s);

				// Fade out near expiry
				float lifeLeft = e.notification.ttl - e.notification.lifetime;
				float fadeOut = lifeLeft < 0.35f ? Mathf.Clamp01(lifeLeft / 0.35f) : 1f;
				float alpha = slideT * fadeOut;

				float boxX = Screen.width - w - margin;
				// Slot 0 sits just above bottom margin; higher slots stack upward
				float baseY = Screen.height - margin - h - e.animY * (h + gap);
				float boxY = baseY + slideOffset;

				Color prev = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				RenderNotification(e.notification, new Rect(boxX, boxY, w, h), i);
				GUI.color = prev;
			}
		}

		private void RenderNotification(Notification notification, Rect win, int index)
		{
			float s = Mathf.Max(0.5f, MainUI.scale);

			BaneGui.Shadow(win);
			BaneGui.DrawWindowBg(win);

			float pad = BoxPad;
			var titleStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = (int)(13f * s),
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = Styles.currentPrimaryColor },
				clipping = TextClipping.Clip
			};
			GUI.Label(new Rect(win.x + pad, win.y + 4f * s, win.width - pad * 2f, 18f * s), notification.title, titleStyle);

			var bodyStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = (int)(12f * s),
				alignment = TextAnchor.UpperLeft,
				wordWrap = true,
				normal = { textColor = new Color(0.93f, 0.93f, 0.94f, 1f) },
				clipping = TextClipping.Clip
			};
			float bodyTop = win.y + BoxHeaderSize.y;
			float bodyH = win.height - BoxHeaderSize.y - BoxSliderSize.y - 2f * s;
			GUI.Label(new Rect(win.x + pad, bodyTop, win.width - pad * 2f, bodyH), notification.message, bodyStyle);

			float remaining = Mathf.Max(0f, notification.ttl - notification.lifetime);
			Rect sliderRect = new Rect(win.x, win.y + win.height - BoxSliderSize.y, win.width, BoxSliderSize.y);
			Controls.DrawSliderInRect(sliderRect, remaining, 0f, Mathf.Max(0.01f, notification.ttl), interactive: false, animKey: "notif_" + index);
		}

		public void Send(string title, string message, float ttl = 5f)
		{
			if (disableNotifications) return;

			var entry = new Entry
			{
				notification = new Notification(title, message, ttl),
				spawnTime = Time.realtimeSinceStartup,
				// Start just below slot 0 so it slides up into place
				animY = -1.2f,
				targetSlot = 0
			};
			// Existing notifications shift up one slot
			for (int i = 0; i < entries.Count; i++)
				entries[i].targetSlot = i + 1;

			entries.Insert(0, entry);
		}

		public void ClearNotifications()
		{
			entries.Clear();
		}
	}
}
