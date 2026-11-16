using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BaneMenu
{
	/// <summary>
	/// Persistent player whitelist keyed by PUID (not name), so it survives name changes and new lobbies.
	/// Whitelisted players are skipped by every "affect all players" action.
	/// Stored in BepInEx/config/BaneMenu_Whitelist.json (kept outside the BaneMenu/ config folder
	/// so it is not mistaken for a menu config profile, and is shared across all profiles).
	/// </summary>
	internal static class Whitelist
	{
		private static readonly string FILE_PATH = Path.Combine(Paths.ConfigPath, "BaneMenu_Whitelist.json");

		// PUID -> last known name (name is only for display/readability of the file)
		private static readonly Dictionary<string, string> entries = new Dictionary<string, string>();
		private static bool loaded = false;

		private static void EnsureLoaded()
		{
			if(loaded) return;
			loaded = true;

			try
			{
				if(!File.Exists(FILE_PATH)) return;

				var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FILE_PATH));
				if(data == null) return;

				foreach(var kvp in data)
				{
					if(!string.IsNullOrEmpty(kvp.Key)) entries[kvp.Key] = kvp.Value ?? "";
				}

				Bane.Log.LogInfo($"Loaded {entries.Count} whitelisted player(s)");
			}
			catch(Exception e)
			{
				Bane.Log.LogError($"Failed to load whitelist: {e.Message}");
			}
		}

		private static void Save()
		{
			try
			{
				var options = new JsonSerializerOptions { WriteIndented = true };
				File.WriteAllText(FILE_PATH, JsonSerializer.Serialize(entries, options));
			}
			catch(Exception e)
			{
				Bane.Log.LogError($"Failed to save whitelist: {e.Message}");
			}
		}

		private static string GetPuid(PlayerControl player)
		{
			if(player == null || player.Data == null) return null;

			string puid = player.Data.Puid;
			return string.IsNullOrEmpty(puid) ? null : puid;
		}

		/// <summary>True if the player has a usable PUID and can therefore be whitelisted.</summary>
		public static bool CanWhitelist(PlayerControl player)
		{
			return GetPuid(player) != null;
		}

		public static bool IsWhitelisted(PlayerControl player)
		{
			EnsureLoaded();

			string puid = GetPuid(player);
			if(puid == null || !entries.ContainsKey(puid)) return false;

			// Keep the stored name fresh in case they renamed
			string name = player.Data.PlayerName ?? "";
			if(name.Length != 0 && entries[puid] != name)
			{
				entries[puid] = name;
				Save();
			}

			return true;
		}

		/// <summary>
		/// Use in "all players" loops. The local player is never skipped so self-targeted behaviour is unchanged.
		/// </summary>
		public static bool ShouldSkip(PlayerControl player)
		{
			if(player == null || player == PlayerControl.LocalPlayer) return false;
			return IsWhitelisted(player);
		}

		public static void Add(PlayerControl player)
		{
			EnsureLoaded();

			string puid = GetPuid(player);
			if(puid == null) return;

			entries[puid] = player.Data.PlayerName ?? "";
			Save();
		}

		public static void Remove(PlayerControl player)
		{
			EnsureLoaded();

			string puid = GetPuid(player);
			if(puid == null) return;

			if(entries.Remove(puid)) Save();
		}

		/// <summary>True if anyone currently in the lobby (other than us) is whitelisted.</summary>
		public static bool AnyWhitelistedInLobby()
		{
			foreach(PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if(ShouldSkip(player)) return true;
			}

			return false;
		}
	}
}
