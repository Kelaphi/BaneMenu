using Hazel;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace BaneMenu.modules.host
{
	/// <summary>
	/// Detect itch.io + ModMenuCrew / Word## bots.
	/// Host: ban. Non-host: leave lobby.
	/// State resets every new lobby so it keeps working.
	/// </summary>
	internal class BotDetector : Module
	{
		public BotDetector() : base("Bot Detector") { }

		readonly HashSet<int> _mmcClients = new HashSet<int>();
		readonly HashSet<int> _handled = new HashSet<int>();
		bool _leaving;
		int _lastGameId;

		static readonly Regex NamePattern = new Regex(
			@"^[A-Za-z]{3,}\d{2}$",
			RegexOptions.Compiled);

		static BotDetector Instance => ModuleManager.botDetector;

		protected override void OnEnable()
		{
			_leaving = false;
			EventCoordinator.OnPlayerJoin += OnJoin;
			Bane.notifications.Send(
				"Bot Detector",
				"On - host bans bots; otherwise leaves the lobby.",
				4);
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnPlayerJoin -= OnJoin;
			_leaving = false;
			_handled.Clear();
			_mmcClients.Clear();
			Bane.notifications.Send("Bot Detector", "Off.", 2);
		}

		void ResetSession(string why)
		{
			_leaving = false;
			_handled.Clear();
			_mmcClients.Clear();
			Bane.Log.LogInfo("BotDetector: session reset (" + why + ")");
		}

		/// <summary>Call when game id / lobby changes so a previous leave doesn't block forever.</summary>
		public void Tick()
		{
			if (!Enabled) return;
			if (AmongUsClient.Instance == null) return;

			int gid = 0;
			try { gid = AmongUsClient.Instance.GameId; } catch { }

			var state = AmongUsClient.Instance.GameState;

			// New lobby / new game id
			if (gid != 0 && gid != _lastGameId)
			{
				_lastGameId = gid;
				ResetSession("new GameId=" + gid);
			}

			// Fully out of a game - clear leave lock so next lobby works
			if (state == InnerNetClient.GameStates.NotJoined)
			{
				if (_leaving || _handled.Count > 0 || _mmcClients.Count > 0)
				{
					_lastGameId = 0;
					ResetSession("NotJoined");
				}
			}
		}

		void OnJoin(PlayerControl player, ClientData client)
		{
			if (!Enabled) return;
			if (player == null || client == null) return;
			if (player == PlayerControl.LocalPlayer) return;

			// If we somehow still have leave lock while joined, clear it
			if (_leaving && IsInLobby())
			{
				_leaving = false;
				Bane.Log.LogInfo("BotDetector: cleared stale _leaving while in lobby");
			}

			if (AmongUsClient.Instance != null)
				AmongUsClient.Instance.StartCoroutine(CoCheck(player, client).WrapToIl2Cpp());
		}

		static bool IsInLobby()
		{
			if (AmongUsClient.Instance == null) return false;
			var s = AmongUsClient.Instance.GameState;
			return s == InnerNetClient.GameStates.Joined || s == InnerNetClient.GameStates.Started;
		}

		System.Collections.IEnumerator CoCheck(PlayerControl player, ClientData client)
		{
			yield return new WaitForSecondsRealtime(0.6f);
			if (!Enabled) yield break;
			if (player == null || client == null) yield break;
			HandleBot(player, client, "join-check");
		}

		public void HandleBot(PlayerControl player, ClientData client, string reason)
		{
			if (!Enabled) return;
			if (player == null || client == null) return;
			if (player == PlayerControl.LocalPlayer) return;

			// Stale leave flag while we're clearly in a lobby again
			if (_leaving && IsInLobby())
				_leaving = false;

			if (_leaving) return;

			int cid = client.Id;
			if (_handled.Contains(cid)) return;

			bool itch = IsItch(client);
			string name = null;
			try { name = player.Data != null ? player.Data.PlayerName : client.PlayerName; } catch { }
			if (string.IsNullOrEmpty(name))
				try { name = client.PlayerName; } catch { }

			bool nameLooksBot = !string.IsNullOrEmpty(name) && NamePattern.IsMatch(name.Trim());
			bool mmc = _mmcClients.Contains(cid);

			if (!itch) return;
			if (!mmc && !nameLooksBot) return;

			_handled.Add(cid);
			string label = name ?? ("client " + cid);
			Bane.Log.LogInfo($"BotDetector hit {label} itch={itch} mmc={mmc} nameBot={nameLooksBot} host={Utilities.IsHostEffective()} ({reason})");

			if (Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Bot Detector", $"Banned bot: {label}", 5);
				try
				{
					Utilities.HostBanPlayer(player);
				}
				catch (System.Exception e)
				{
					Bane.Log.LogWarning("BotDetector ban: " + e.Message);
					try { AmongUsClient.Instance?.KickPlayer(cid, true); } catch { }
				}
				return;
			}

			_leaving = true;
			Bane.notifications.Send(
				"Bot Detector",
				"Bot detected, leaving because that guys a chud",
				6);
			try
			{
				AmongUsClient.Instance?.ExitGame(DisconnectReasons.ExitGame);
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("BotDetector leave: " + e.Message);
				_leaving = false;
			}
		}

		public static bool IsItch(ClientData client)
		{
			if (client == null) return false;
			try
			{
				var pd = client.PlatformData;
				if (pd == null) return false;
				if (pd.Platform == Platforms.StandaloneItch) return true;
				string pn = pd.PlatformName ?? "";
				if (pn.IndexOf("itch", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			catch { }
			return false;
		}

		public void MarkMmc(int clientId)
		{
			if (clientId < 0) return;
			_mmcClients.Add(clientId);
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		static class MmcRpcPatch
		{
			static void Postfix(PlayerControl __instance, byte callId, MessageReader reader)
			{
				if (callId != 202) return;
				if (__instance == null || reader == null) return;
				if (Instance == null || !Instance.Enabled) return;

				try
				{
					int pos = reader.Position;
					string a = null;
					string b = null;
					try { a = reader.ReadString(); } catch { }
					try { b = reader.ReadString(); } catch { }
					try { reader.Position = pos; } catch { }

					bool mmc = (!string.IsNullOrEmpty(a) && a.IndexOf("MMC", System.StringComparison.OrdinalIgnoreCase) >= 0)
						|| (!string.IsNullOrEmpty(b) && b.IndexOf("MMC", System.StringComparison.OrdinalIgnoreCase) >= 0);
					if (!mmc) return;

					int cid = -1;
					try
					{
						if (AmongUsClient.Instance != null)
							cid = AmongUsClient.Instance.GetClientIdFromCharacter(__instance);
					}
					catch { }
					if (cid < 0 && __instance.Data != null)
						try { cid = __instance.Data.ClientId; } catch { }

					if (cid >= 0)
						Instance.MarkMmc(cid);

					ClientData client = null;
					try { client = AmongUsClient.Instance?.GetClientFromCharacter(__instance); } catch { }
					if (client == null && cid >= 0)
						try { client = AmongUsClient.Instance?.FindClientById(cid); } catch { }

					Instance.HandleBot(__instance, client, "mmc-rpc");
				}
				catch { }
			}
		}
	}
}
