using BaneMenu.assets;
﻿using BaneMenu.modules;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class SpooferSection : Section
	{
		public SpooferSection() : base("Spoofer") { }

		public readonly Dictionary<string, int> versions = new Dictionary<string, int>()
		{
			// Current version at runtime
			// VersionShower::Start uses ReferenceDataManager.Refdata.userFacingVersion to get version strings such as "17.1"
			// however that doesn't seem to before the game fully loads, so we have to use Constants::AddressablesVersion to get a less human-understandable version string
			{ $"{Constants.AddressablesVersion} (Current)", Constants.GetBroadcastVersion() },
			{ "16.1.0", 50632950 },
			{ "17.1", 50643450 },
			{ "17.1.2", 50647000 },
			{ "17.2", 50645050 },
			{ "17.2.1", 50652900 },
			{ "17.2.2", 50653700 },
			{ "17.3", 50652400 },
			{ "17.4", 50656300 },
			{ "18.0", 50663350 },
			{ "18.0 (build 7348)", 50663400 },
			{ "19.0 (build 7488)", 50663650 }, // iOS
			{ "19.0 (build 7489)", 50663600 } // PC
		};

		private int versionSelection = 0;

		public override void Render()
		{
GUILayout.Label("Version Spoofer:");
			ModuleManager.spoofVersion.Enabled = Controls.Toggle(ModuleManager.spoofVersion.Enabled, "Enable Version Spoofing");

			{
				var verNames = versions.Keys.ToArray();
				if (versionSelection < 0) versionSelection = 0;
				if (versionSelection >= verNames.Length) versionSelection = verNames.Length - 1;
				int pickedVer = Controls.Dropdown("Spoofed Version", verNames, versionSelection, "spoof_version_dd");
				if (pickedVer != versionSelection)
					versionSelection = pickedVer;
				ModuleManager.spoofVersion.spoofedVersion = versions.ElementAt(versionSelection).Value;
				GUILayout.Label($"Broadcast: {ModuleManager.spoofVersion.spoofedVersion}");
			}

			ModuleManager.spoofVersion.UseModdedProtocol = Controls.Toggle(ModuleManager.spoofVersion.UseModdedProtocol, "Use Modded Protocol");

			// AMCI mod registration GUID spoof (BaneMenu 2.2.0 feature, early port)
			var presets = BaneMenu.modules.spoofer.SpoofVersion.ModRegistrationPresets;
			int modIdx = ModuleManager.spoofVersion.ModRegistrationIndex;
			if (modIdx < 0) modIdx = 0;
			if (modIdx >= presets.Length) modIdx = presets.Length - 1;
			var modNames = new string[presets.Length];
			for (int mi = 0; mi < presets.Length; mi++) modNames[mi] = presets[mi].Name;
			int newIdx = Controls.Dropdown("Spoof Mod Registration", modNames, modIdx, "spoof_modreg_dd");
			if (newIdx != ModuleManager.spoofVersion.ModRegistrationIndex)
			{
				ModuleManager.spoofVersion.ModRegistrationIndex = newIdx;
				BaneMenu.modules.spoofer.SpoofVersion.ApplyModRegistration();
				Bane.notifications.Send("Mod Registration", $"Spoofing: {presets[newIdx].Name}", 4);
			}

			GUILayout.Space(5);
			GUILayout.Label("Level Spoofer:");

			ModuleManager.spoofLevel.Enabled = Controls.Toggle(ModuleManager.spoofLevel.Enabled, "Enabled");
			GUILayout.Label($"Spoofed Level: {ModuleManager.spoofLevel.SpoofedLevel}");
			ModuleManager.spoofLevel.SpoofedLevel = (uint)Controls.HorizontalSlider(ModuleManager.spoofLevel.SpoofedLevel, 1, 200);

			GUILayout.BeginHorizontal();
			if(Controls.Button("-100"))
			{
				ClampSelectedLevel(ModuleManager.spoofLevel.SpoofedLevel - 100);
			}

			if(Controls.Button("-10"))
			{
				ClampSelectedLevel(ModuleManager.spoofLevel.SpoofedLevel - 10);
			}

			if(Controls.Button("+10"))
			{
				ClampSelectedLevel(ModuleManager.spoofLevel.SpoofedLevel + 10);
			}

			if(Controls.Button("+100"))
			{
				ClampSelectedLevel(ModuleManager.spoofLevel.SpoofedLevel + 100);
			}
			GUILayout.EndHorizontal();

			if(Controls.Button("Send Level Update"))
			{
				PlayerControl.LocalPlayer.RpcSetLevel(ModuleManager.spoofLevel.SpoofedLevel - 1);
				Bane.notifications.Send("Level Updater", $"Your level has been changed to {ModuleManager.spoofLevel.SpoofedLevel}", 5);
			}

			GUILayout.Space(5);
			GUILayout.Label("Platform Spoofer:");

			{
				var platKeys = GameAssets.PlayerPlatforms.Keys.ToList();
				var platNames = new string[platKeys.Count];
				for (int pi = 0; pi < platKeys.Count; pi++)
					platNames[pi] = GameAssets.PlayerPlatforms[platKeys[pi]];
				int curPlat = platKeys.IndexOf((int)ModuleManager.spoofDevice.SpoofedPlatform);
				if (curPlat < 0) curPlat = 0;
				int pickPlat = Controls.Dropdown("Spoofed Platform", platNames, curPlat, "spoof_platform_dd");
				ModuleManager.spoofDevice.SpoofedPlatform = (Platforms)platKeys[Math.Clamp(pickPlat, 0, platKeys.Count - 1)];
			}
		}

		private void ClampSelectedLevel(uint newLevel)
		{
			// Do we really need to have an upper bounds on the level value?
			// I doubt anyone will press the +100 that much anyway
			uint maxLevel = Utilities.IsAnticheatPresent() ? 100001 : uint.MaxValue - 1;

			ModuleManager.spoofLevel.SpoofedLevel = Math.Clamp(newLevel, 0, maxLevel);
		}
	}
}