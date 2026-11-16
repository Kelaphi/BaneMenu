using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.anticheat
{
	/// <summary>
	/// Detect known mod menus by out-of-range / signature custom RPC callIds
	/// (same approach as Hydralum ModDetection / Krush fingerprints).
	/// </summary>
	internal static class ModMenuCheck
	{
		public static bool Enabled { get; set; } = true;

		/// <summary>When false, RPC 164 (Sicko / Hydra fingerprint) is ignored.</summary>
		public static bool DetectSicko { get; set; } = true;

		// Aligned with Hydralum ModDetection KnownMods (Krush fingerprints)
		static readonly Dictionary<byte, string> KnownMods = new Dictionary<byte, string>
		{
			{ 121, "Chocoo" },
			{ 167, "TuffMenu" },
			// SickoMenu InnerNetClient: StartRpcImmediately(..., (uint8_t)420, ...) → byte 164 + version string
			{ 164, "SickoMenu" },
			{ 176, "HostGuard / TOH" },
			{ 195, "Polar" },
			{ 204, "Polar" },
			{ 154, "GNC" },
			{ 250, "KillNet" },
			{ 80,  "KillNet" },
			{ 85,  "KillNet" },
			{ 150, "BetterAmongUs / GreaterAmongUs" },
			{ 82,  "Unknown (RPC 82)" },
			{ 103, "Unknown (Askinchik)" },
			{ 212, "BanMod" },
			{ 213, "BanMod" },
			{ 214, "BanMod" },
			{ 215, "BanMod" },
			{ 216, "BanMod" },
			{ 217, "BanMod" },
			{ 218, "BanMod" },
			{ 144, "Gaff Menu" },
			{ 145, "Gaff Menu" },
			{ 188, "GMM" },
			{ 189, "GMM" },
			{ 169, "Malum Menu" },
			{ 133, "Lunar / NjordMenu" },
			{ 210, "Lunar / NjordMenu" },
			{ 89,  "NjordMenu (Old)" },
			{ 202, "ModMenuCrew" },
			{ 162, "Unknown (RPC 162)" },
			{ 255, "Unknown (RPC 255)" },
		};

		// Vanilla RpcCalls max is ~66; 67 = SpiritGuide in newer builds
		static readonly HashSet<byte> ExtraVanilla = new HashSet<byte> { 67 };

		// playerId → set of labels
		static readonly Dictionary<byte, HashSet<string>> Detected = new Dictionary<byte, HashSet<string>>();

		public static void Reset() => Detected.Clear();

		public static bool TryGet(byte playerId, out string label)
		{
			label = null;
			if (!Detected.TryGetValue(playerId, out var set) || set.Count == 0) return false;
			label = string.Join(", ", set);
			return true;
		}

		public static void Observe(PlayerControl player, byte callId)
		{
			// Independent of Anticheat.Enabled - this toggle alone controls detection
			if (!Enabled || player == null || player.AmOwner) return;
			if (player.Data == null) return;

			// Vanilla enum values are fine
			if (System.Enum.IsDefined(typeof(RpcCalls), callId)) return;
			if (ExtraVanilla.Contains(callId)) return;

			// Optional Sicko / Hydra fingerprint (callId 164)
			if (callId == 164 && !DetectSicko) return;

			string name;
			if (!KnownMods.TryGetValue(callId, out name))
				name = $"Modded (RPC {callId})";

			byte pid = player.PlayerId;
			if (!Detected.TryGetValue(pid, out var set))
			{
				set = new HashSet<string>();
				Detected[pid] = set;
			}
			if (!set.Add(name)) return; // already known

			string playerName = player.Data != null ? player.Data.PlayerName : $"Player {pid}";
			// Notify via our own path so AC being off does not silence detections
			Bane.notifications.Send("Mod Menu Check", $"{playerName} appears to be using {name} (custom RPC {callId}).", 8f);
			// Still allow AC punishment path when the anticheat itself is on
			if (Anticheat.Enabled)
				Anticheat.Flag(player, $"{playerName} appears to be using {name} (custom RPC {callId}).");
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		class ObservePlayerControlRpc
		{
			static void Postfix(PlayerControl __instance, byte callId)
			{
				Observe(__instance, callId);
			}
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
		class ClearOnExit
		{
			static void Postfix() => Reset();
		}
	}
}
