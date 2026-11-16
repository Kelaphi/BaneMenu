using System;
using UnityEngine;
using BaneMenu.modules;

namespace BaneMenu.ui.sections
{
	internal class MenuSection : Section
	{
		// Draft values while dragging - applied only on slider release
		static float _draftPrimary = -1f;
		static float _draftScale = -1f;

		public MenuSection() : base("Settings") { }

		public override void Render()
		{
			Bane.notifications.disableNotifications = Controls.Toggle(Bane.notifications.disableNotifications, "Disable Notifications");
			ModuleManager.chatLogger.Enabled = Controls.Toggle(ModuleManager.chatLogger.Enabled, "Log chat messages to console");
			if (Controls.Button("Clear Notifications"))
			{
				Bane.notifications.ClearNotifications();
				Bane.notifications.Send("Notifications", "All notifications have been cleared.", 5f);
			}
			GUILayout.Space(6);

			// Primary color - dropdown
			{
				var colorNames = new string[Styles.ColorValues.Count];
				int ci = 0;
				foreach (var kv in Styles.ColorValues)
					colorNames[ci++] = kv.Key.ToString();
				int cur = (int)Styles.primaryColor;
				if (cur < 0) cur = 0;
				if (cur >= colorNames.Length) cur = colorNames.Length - 1;
				int picked = Controls.Dropdown("Primary Color", colorNames, cur, "settings_primary_color");
				if (picked != cur)
				{
					Styles.primaryColor = (Styles.UIColors)picked;
					Styles.SyncPrimaryFromEnum();
					Styles.ClearCache();
					Bane.config?.MarkDirty();
					_draftPrimary = picked;
				}
			}

			// UI scale - apply only on release
			if (_draftScale < 0f) _draftScale = MainUI.scale;
			GUILayout.Label($"UI Scale: {_draftScale:F2}x");
			float newScale = Controls.HorizontalSliderDeferred(_draftScale, 0.5f, 2.0f, out bool scaleReleased);
			_draftScale = (float)Math.Round(newScale, 2);
			if (scaleReleased)
			{
				float next = Mathf.Clamp(_draftScale, 0.5f, 2.0f);
				if (Mathf.Abs(next - MainUI.scale) > 0.001f)
				{
					MainUI.scale = next;
					Styles.ClearCache();
					Bane.config?.MarkDirty();
				}
				_draftScale = MainUI.scale;
			}

			GUILayout.Space(5);
			GUILayout.Label("Testing:");
			bool dev = Controls.Toggle(BaneMenu.modules.DevUnlock.Enabled, "Unlock all BaneMenu gates");
			if (dev != BaneMenu.modules.DevUnlock.Enabled)
			{
				BaneMenu.modules.DevUnlock.Enabled = dev;
				Bane.config?.MarkDirty();
				Bane.notifications.Send(
					"Dev Unlock",
					dev ? "BaneMenu host/AC gates off (game AmHost unchanged)." : "Normal BaneMenu checks restored.",
					5);
			}
			GUILayout.Label("(This option should only be used if you are inside a Host-Auth lobby)");

			GUILayout.Space(6);
			GUILayout.Label("--- AC Watch ---");
			GUILayout.Space(6);
			GUILayout.Label("--- RPC Logger ---");
			ModuleManager.rpcLogger.Enabled = Controls.Toggle(ModuleManager.rpcLogger.Enabled, "Log all RPCs to console");
			ModuleManager.rpcLogger.LogOutgoing = Controls.Toggle(ModuleManager.rpcLogger.LogOutgoing, "Log OUT (sent)");
			ModuleManager.rpcLogger.LogIncoming = Controls.Toggle(ModuleManager.rpcLogger.LogIncoming, "Log IN (received)");
			ModuleManager.rpcLogger.SkipSnapTo = Controls.Toggle(ModuleManager.rpcLogger.SkipSnapTo, "Skip SnapTo spam");
			ModuleManager.rpcLogger.SkipPet = Controls.Toggle(ModuleManager.rpcLogger.SkipPet, "Skip Pet spam");
			ModuleManager.rpcLogger.DumpHex = Controls.Toggle(ModuleManager.rpcLogger.DumpHex, "Dump payload hex");
			GUILayout.Label("BepInEx: [RPC IN/OUT] - for ban/AC reverse engineering");

			ModuleManager.anticheatWatch.Enabled = Controls.Toggle(ModuleManager.anticheatWatch.Enabled, "Watch real anticheat (0.5s)");
			ModuleManager.anticheatWatch.LogEveryPoll = Controls.Toggle(ModuleManager.anticheatWatch.LogEveryPoll, "Log every poll to console");
			GUILayout.Label("Uses Data.OwnerId (ignores Unlock gates). Notifies when AC drops.");
			if (ModuleManager.anticheatWatch.Enabled)
			{
				var posture = BaneMenu.modules.misc.AnticheatWatch.Evaluate(out string detail);
				GUILayout.Label(BaneMenu.modules.misc.AnticheatWatch.PostureLabel(posture));
				GUILayout.Label(detail);
			}

			if (Controls.Button("Eject"))
			{
				Bane.Eject();
			}

		}
	}
}
