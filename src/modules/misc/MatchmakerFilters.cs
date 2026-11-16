using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine.Networking;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// Server-side matchmaker filters: rewrites GET /api/games/filtered?filter=...
	/// Injects MaxPlayers (int) and/or TrueHostName into FilterSets JSON.
	/// </summary>
	internal class MatchmakerFilters : Module
	{
		public MatchmakerFilters() : base("Matchmaker Filters") { }

		public bool FilterEnabled = false;
		public bool UseMaxPlayersFilter = false;
		public int MaxPlayersFilter = 15;
		public bool UseHostNameFilter = false;
		public string HostNameSearch = "";
		public bool UseMinPlayerCount = false;
		public int MinPlayerCount = 0;

		public static string LastRewrittenUrl = "";

		public static string BuildIntSubFilter(int value)
		{
			return "{\"AcceptedValues\":[" + value + "],\"OptionEnum\":1,\"FilterType\":\"int\"}";
		}

		public static string BuildStringSubFilter(string value)
		{
			string escaped = EscapeJson(value ?? "");
			return "{\"AcceptedValues\":\"" + escaped + "\",\"FilterType\":\"string\"}";
		}

		static string EscapeJson(string s)
		{
			if (s == null) return "";
			return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		public static string InjectIntoFilterJson(string filterJson, MatchmakerFilters mod)
		{
			if (string.IsNullOrEmpty(filterJson) || mod == null || !mod.FilterEnabled)
				return filterJson;

			var extra = new List<string>();

			if (mod.UseMaxPlayersFilter && mod.MaxPlayersFilter > 0)
			{
				string sub = EscapeJson(BuildIntSubFilter(mod.MaxPlayersFilter));
				extra.Add("{\"OptionType\":\"int\",\"Key\":\"MaxPlayers\",\"SubFilterString\":\"" + sub + "\"}");
			}

			if (mod.UseHostNameFilter && !string.IsNullOrWhiteSpace(mod.HostNameSearch))
			{
				string sub = EscapeJson(BuildStringSubFilter(mod.HostNameSearch.Trim()));
				extra.Add("{\"OptionType\":\"string\",\"Key\":\"TrueHostName\",\"SubFilterString\":\"" + sub + "\"}");
			}

			if (extra.Count == 0)
				return filterJson;

			string inject = string.Join(",", extra) + ",";
			var sb = new StringBuilder();
			int i = 0;
			const string key = "\"Filters\":[";
			while (i < filterJson.Length)
			{
				int at = filterJson.IndexOf(key, i, StringComparison.Ordinal);
				if (at < 0)
				{
					sb.Append(filterJson, i, filterJson.Length - i);
					break;
				}
				sb.Append(filterJson, i, at - i);
				sb.Append(key);
				sb.Append(inject);
				i = at + key.Length;
			}

			Bane.Log.LogInfo("[MatchmakerFilters] server inject MaxPlayers=" +
			                (mod.UseMaxPlayersFilter ? mod.MaxPlayersFilter.ToString() : "off") +
			                " TrueHostName=" +
			                (mod.UseHostNameFilter ? mod.HostNameSearch : "off"));
			return sb.ToString();
		}

		public static string RewriteFilteredUrl(string url, MatchmakerFilters mod)
		{
			if (string.IsNullOrEmpty(url) || mod == null || !mod.FilterEnabled)
				return url;
			if (url.IndexOf("games/filtered", StringComparison.OrdinalIgnoreCase) < 0)
				return url;

			try
			{
				int q = url.IndexOf('?');
				if (q < 0) return url;

				string baseUrl = url.Substring(0, q);
				string query = url.Substring(q + 1);
				string filterVal = null;
				var otherParts = new List<string>();

				foreach (var part in query.Split('&'))
				{
					int eq = part.IndexOf('=');
					if (eq <= 0) { otherParts.Add(part); continue; }
					string name = part.Substring(0, eq);
					string val = part.Substring(eq + 1);
					if (name.Equals("filter", StringComparison.OrdinalIgnoreCase))
						filterVal = Uri.UnescapeDataString(val.Replace("+", " "));
					else
						otherParts.Add(part);
				}

				if (filterVal == null)
					return url;

				string newFilter = InjectIntoFilterJson(filterVal, mod);
				if (newFilter == filterVal)
					return url;

				string encoded = Uri.EscapeDataString(newFilter);
				var qb = new StringBuilder();
				qb.Append(baseUrl).Append("?filter=").Append(encoded);
				foreach (var p in otherParts)
					qb.Append('&').Append(p);

				string rewritten = qb.ToString();
				LastRewrittenUrl = rewritten;
				Bane.Log.LogInfo("[MatchmakerFilters] rewrote /api/games/filtered URL");
				return rewritten;
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning("[MatchmakerFilters] URL rewrite failed: " + e.Message);
				return url;
			}
		}

		[HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.SendWebRequest))]
		static class PatchSendWebRequest
		{
			static void Prefix(UnityWebRequest __instance)
			{
				try
				{
					var mod = ModuleManager.matchmakerFilters;
					if (mod == null || !mod.FilterEnabled) return;
					if (__instance == null) return;

					string url = __instance.url;
					if (string.IsNullOrEmpty(url)) return;
					if (url.IndexOf("games/filtered", StringComparison.OrdinalIgnoreCase) < 0) return;

					string rewritten = RewriteFilteredUrl(url, mod);
					if (rewritten != url)
						__instance.url = rewritten;
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning("[MatchmakerFilters] SendWebRequest: " + e.Message);
				}
			}
		}

		[HarmonyPatch(typeof(UnityWebRequest), "set_url")]
		static class PatchSetUrl
		{
			static void Prefix(ref string value)
			{
				try
				{
					var mod = ModuleManager.matchmakerFilters;
					if (mod == null || !mod.FilterEnabled) return;
					if (string.IsNullOrEmpty(value)) return;
					if (value.IndexOf("games/filtered", StringComparison.OrdinalIgnoreCase) < 0) return;
					value = RewriteFilteredUrl(value, mod);
				}
				catch { }
			}
		}
	}
}
