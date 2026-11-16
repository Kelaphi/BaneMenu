using AmongUs.Data.Player;
using HarmonyLib;
using InnerNet;

namespace BaneMenu.modules.protections
{
	/// <summary>
	/// Ignore client "You are banned for hacking. Please stop." ban handling.
	/// Does NOT block DisconnectReasons.Hacking kicks.
	/// </summary>
	internal class IgnoreHackingBan : Module
	{
		public IgnoreHackingBan() : base("Ignore Hacking Ban Message") { }

		static IgnoreHackingBan Inst => ModuleManager.ignoreHackingBan;

		internal static bool IsHackingBanMessage(string msg)
		{
			if (string.IsNullOrEmpty(msg)) return false;
			return msg.IndexOf("banned for hacking", System.StringComparison.OrdinalIgnoreCase) >= 0
			       || (msg.IndexOf("Please stop", System.StringComparison.OrdinalIgnoreCase) >= 0
			           && msg.IndexOf("hack", System.StringComparison.OrdinalIgnoreCase) >= 0);
		}

		internal static bool ShouldBlock(DisconnectReasons reason, string stringReason)
		{
			if (Inst == null || !Inst.Enabled) return false;
			if (reason == DisconnectReasons.Hacking) return false; // never block real Hacking kicks
			return IsHackingBanMessage(stringReason);
		}

		internal static void OnBlocked(DisconnectReasons reason, string stringReason)
		{
			Bane.Log.LogInfo("[IgnoreHackingBan] blocked: " + reason + " | " + stringReason);
			Bane.notifications.Send("Ignore Hacking Ban", "Blocked ban message (not Hacking kick).", 4);
			try
			{
				var ban = AmongUs.Data.DataManager.Player?.ban;
				if (ban != null) ban.banPoints = 0f;
			}
			catch { }
		}

		[HarmonyPatch(typeof(PlayerBanData), nameof(PlayerBanData.BanMinutesLeft), MethodType.Getter)]
		static class PatchBanMinutes
		{
			static void Postfix(PlayerBanData __instance, ref int __result)
			{
				if (Inst == null || !Inst.Enabled) return;
				try
				{
					__instance.banPoints = 0f;
					__result = 0;
				}
				catch { }
			}
		}

		// Signature: (DisconnectReasons, string)
		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.EnqueueDisconnect),
			new[] { typeof(DisconnectReasons), typeof(string) })]
		static class PatchEnqueue2
		{
			static bool Prefix(DisconnectReasons reason, string stringReason)
			{
				if (!ShouldBlock(reason, stringReason)) return true;
				OnBlocked(reason, stringReason);
				return false;
			}
		}

		// Signature: (DisconnectReasons) only - cannot see message; do not block (avoid eating real kicks)
		// no patch
	}
}
