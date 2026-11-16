using HarmonyLib;
using System;

namespace BaneMenu.modules.spoofer
{
	internal class SpoofVersion : Module
	{
		public SpoofVersion() : base("SpoofVersion") { }

		public int spoofedVersion = Constants.GetBroadcastVersion();
		public bool UseModdedProtocol { get; set; } = false;

		/// <summary>
		/// Index into known mod registration presets (0 = none).
		/// Spoofs CurrentModRegistration.ModRegistrationGuidString so Find Game
		/// returns that mod's host-auth lobbies (e.g. Endless Host Roles).
		/// </summary>
		public int ModRegistrationIndex { get; set; } = 0;

		public static readonly (string Name, string Guid)[] ModRegistrationPresets =
		{
			// From BaneMenu 2.2.0 / commit 004036b - blank = no mod registered
			("None", ""),
			// https://github.com/Gurge44/EndlessHostRoles/.../AmciRegistration.cs
			("Endless Host Roles", "446420cc-ce35-4c2f-8f5d-b14f84354421"),
			// https://github.com/TownOfNext/TownOfNext/.../main.cs
			("Town of Next", "789bf6f3-41e4-439f-82d3-ed6d2d2fb710"),
			// https://github.com/SuperNewRoles/SuperNewRoles/.../statics.cs
			("Super New Roles", "9f21dfdc-1224-4322-af7b-63db54cea706"),
			// Town of Host Optimized (commented upstream - enable if you need it)
			// ("Town of Host Optimized", "995c2cea-a12e-418b-a500-6436eefaaf4d"),
		};

		private static SpoofVersion Instance => ModuleManager.spoofVersion;

		private static string lastAppliedGuid = null;

		public static string CurrentPresetName
		{
			get
			{
				int i = Instance.ModRegistrationIndex;
				if (i < 0 || i >= ModRegistrationPresets.Length) return "None";
				return ModRegistrationPresets[i].Name;
			}
		}

		public static void ApplyModRegistration()
		{
			try
			{
				int i = Instance.ModRegistrationIndex;
				if (i < 0 || i >= ModRegistrationPresets.Length) i = 0;

				string guid = ModRegistrationPresets[i].Guid;
				if (guid == null) guid = "";

				// Only write when changed to avoid spam
				if (lastAppliedGuid == guid) return;

				CurrentModRegistration.ModRegistrationGuidString = guid;
				lastAppliedGuid = guid;

				if (string.IsNullOrEmpty(guid))
					Bane.Log.LogInfo("AMCI: cleared ModRegistrationGuidString");
				else
					Bane.Log.LogInfo($"AMCI: set ModRegistrationGuidString = {guid} ({ModRegistrationPresets[i].Name})");
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning($"AMCI ApplyModRegistration failed: {e.Message}");
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			ApplyModRegistration();
		}

		protected override void OnDisable()
		{
			// Don't clear GUID on version-spoof disable - user may still want mod filter.
			// Clearing is done by selecting preset "None".
		}

		[HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
		class BroadcastVersion
		{
			static bool Prefix(ref int __result)
			{
				// Starting a local lobby or entering Freeplay will bug out if we are using a spoofed version
				if (!Instance.Enabled || AmongUsClient.Instance == null || AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame) return true;

				__result = Instance.spoofedVersion;
				if (Instance.UseModdedProtocol) __result += 25;

				return false;
			}
		}

		[HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
		class MarkVersionModded
		{
			static bool Prefix(ref bool __result)
			{
				if (!Instance.Enabled || !Instance.UseModdedProtocol) return true;

				__result = true;
				return false;
			}
		}

		// Keep GUID applied even if something else clears it; also drives matchmaking filter
		[HarmonyPatch(typeof(CurrentModRegistration), nameof(CurrentModRegistration.TryGetModRegistrationGuid))]
		class EnsureModGuid
		{
			static void Prefix()
			{
				// Re-apply selected preset so Find Game / Host always see the spoofed GUID
				if (Instance.ModRegistrationIndex > 0)
					ApplyModRegistration();
			}
		}
	}
}
