using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using System.Collections;
using UnityEngine;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// AuraMenu-style quick rejoin: capture lobby endpoint + matchmaker token,
	/// then CoJoinOnlineGameDirect after a short delay (no region/code round-trip).
	/// </summary>
	internal static class QuickRejoin
	{
		public static int LastGameId;
		public static IRegionInfo LastServer;
		public static string LastServerIp;
		public static int LastServerPort;
		public static string MatchmakerToken;

		/// <summary>User-configurable delay for the standalone Quick Rejoin button (Aura: 0.35–1.0).</summary>
		public static float Delay = 0.5f;

		/// <summary>Fixed delay used by Force Host / Kick Host / Anti Start Game.</summary>
		public const float ForcedDelay = 0.5f;

		public static bool HasLobbyData => LastGameId != 0 && !string.IsNullOrEmpty(LastServerIp);

		public static void CaptureFromClient(AmongUsClient client)
		{
			if (client == null) return;
			try
			{
				var inner = (InnerNetClient)client;
				LastGameId = inner.GameId;
				LastServerIp = inner.networkAddress;
				LastServerPort = inner.networkPort;
				if (ServerManager.Instance != null)
					LastServer = ServerManager.Instance.CurrentRegion;
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("QuickRejoin.Capture: " + e.Message);
			}
		}

		/// <summary>AuraMenu Rejoin(true) - direct IP/port/token join.</summary>
		public static void RejoinQuick()
		{
			if (LastGameId == 0 || AmongUsClient.Instance == null) return;
			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameDirect(
						LastGameId,
						LastServerIp,
						(ushort)LastServerPort,
						MatchmakerToken));
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("QuickRejoin.RejoinQuick failed, falling back: " + e.Message);
				RejoinCode();
			}
		}

		/// <summary>AuraMenu Rejoin(false) - exit + code join fallback.</summary>
		public static void RejoinCode()
		{
			if (LastGameId == 0 || AmongUsClient.Instance == null) return;
			try
			{
				AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);
			}
			catch { }
			try
			{
				if (LastServer != null && ServerManager.Instance != null)
					ServerManager.Instance.SetRegion(LastServer);
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(LastGameId));
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("QuickRejoin.RejoinCode: " + e.Message);
			}
		}

		public static void Rejoin(bool quick)
		{
			if (LastGameId == 0) return;
			if (quick && HasLobbyData)
				RejoinQuick();
			else
				RejoinCode();
		}

		/// <summary>
		/// AuraMenu QueueRejoin: wait <paramref name="delay"/>, THEN Rejoin.
		/// Delay is before the join (not after ExitGame). Quick path never ExitGames.
		/// </summary>
		public static IEnumerator QueueRejoin(float delay, bool quick = true)
		{
			if (delay > 0f)
				yield return new WaitForSeconds(delay);
			Rejoin(quick);
		}

		/// <summary>Start QueueRejoin on ModuleManager (survives ExitGame).</summary>
		public static void StartQueue(float delay, bool quick = true)
		{
			if (Bane.modules == null) return;
			// Refresh capture from current session if still in lobby
			if (AmongUsClient.Instance != null && AmongUsClient.Instance.GameId != 0)
				CaptureFromClient(AmongUsClient.Instance);
			Bane.modules.StartCoroutine(QueueRejoin(delay, quick).WrapToIl2Cpp());
		}

		/// <summary>
		/// AuraMenu button path: capture, optional delay, then CoJoinOnlineGameDirect.
		/// Does NOT call ExitGame first - that is what makes the non-quick path slow.
		/// </summary>
		public static void AuraQuickRejoinNow(float delay)
		{
			if (AmongUsClient.Instance != null && AmongUsClient.Instance.GameId != 0)
				CaptureFromClient(AmongUsClient.Instance);
			if (LastGameId == 0)
			{
				Bane.notifications.Send("Quick Rejoin", "No lobby data captured yet.", 3);
				return;
			}
			StartQueue(delay, true);
		}

		/// <summary>Exit then queue quick rejoin after fixed 0.5s (Force Host / Kick Host / Anti Start only).</summary>
		public static void ExitAndQuickRejoinForced()
		{
			if (AmongUsClient.Instance != null && AmongUsClient.Instance.GameId != 0)
				CaptureFromClient(AmongUsClient.Instance);
			try { AmongUsClient.Instance?.ExitGame(DisconnectReasons.ExitGame); }
			catch { }
			StartQueue(ForcedDelay, true);
		}

		// --- Harmony capture patches ---

		[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
		static class CaptureOnJoin
		{
			static void Postfix(AmongUsClient __instance)
			{
				CaptureFromClient(__instance);
			}
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoConnectToGameServer))]
		static class CaptureMmToken
		{
			static void Prefix(ref string matchmakerToken)
			{
				if (!string.IsNullOrEmpty(matchmakerToken))
					MatchmakerToken = matchmakerToken;
			}
		}
	}
}
