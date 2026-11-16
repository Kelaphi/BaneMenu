using BaneMenu.modules;
using BaneMenu.modules.misc;
using System;
using System.Net;
using InnerNet;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	/// <summary>
	/// Join via matchmaker listing fields: IP (uint or dotted), Port, GameId.
	/// Uses BaneGui text boxes (IL2CPP-safe).
	/// </summary>
	internal class LobbySection : Section
	{
		public LobbySection() : base("Lobby") { }

		private uint ipPacked = 0;
		private ushort port = 22023;
		private int gameId = 0;
		private string ipDotted = "0.0.0.0";

		private string ipPackedText = "0";
		private string ipDottedText = "0.0.0.0";
		private string portText = "22023";
		private string gameIdText = "0";
		private string lobbyCodeText = "";

		public override void Render()
		{
			GUILayout.Label("Matchmaker Filters (server-side)");
			GUILayout.Label("Rewrites GET /api/games/filtered?filter=...");
			GUILayout.Label("Injects MaxPlayers (int) + TrueHostName (string).");
			var mf = ModuleManager.matchmakerFilters;
			mf.FilterEnabled = Controls.Toggle(mf.FilterEnabled, "Enable custom Find Game filters");
			if (mf.FilterEnabled)
			{
				mf.UseMaxPlayersFilter = Controls.Toggle(mf.UseMaxPlayersFilter, "Filter by MaxPlayers (lobby capacity)");
				if (mf.UseMaxPlayersFilter)
				{
					GUILayout.Label($"MaxPlayers = {mf.MaxPlayersFilter}");
					mf.MaxPlayersFilter = (int)GUILayout.HorizontalSlider(mf.MaxPlayersFilter, 1, 15);
				}

				mf.UseHostNameFilter = Controls.Toggle(mf.UseHostNameFilter, "Host name search (TrueHostName)");
				if (mf.UseHostNameFilter)
				{
					bool commitHost;
					mf.HostNameSearch = Controls.TextBox("Host name", mf.HostNameSearch ?? "", "mm_hostname", out commitHost, 24);
					GUILayout.Label("Matches TrueHostName / HostName (contains, ignore case)");
				}

				mf.UseMinPlayerCount = Controls.Toggle(mf.UseMinPlayerCount, "Min current player count");
				if (mf.UseMinPlayerCount)
				{
					GUILayout.Label($"Min players in lobby = {mf.MinPlayerCount}");
					mf.MinPlayerCount = (int)GUILayout.HorizontalSlider(mf.MinPlayerCount, 1, 15);
				}

				GUILayout.Label("Open/refresh Find Game after enabling.");
				GUILayout.Label("Log: [MatchmakerFilters] rewrote /api/games/filtered");
			}

			GUILayout.Space(10);
			GUILayout.Label("Join by IP / Port / GameId");
			GUILayout.Label("(from Find Game JSON - no lobby code needed)");
			GUILayout.Space(4);

			bool commit;
			ipPackedText = Controls.TextBox("IP uint", ipPackedText, "lobby_ip_uint", out commit, 16);
			if (commit) ApplyIpUint();
			if (Controls.Button("Apply IP uint"))
				ApplyIpUint();

			ipDottedText = Controls.TextBox("IP dotted", ipDottedText, "lobby_ip_dotted", out commit, 32);
			if (commit) ApplyIpDotted();
			if (Controls.Button("Apply IP dotted"))
				ApplyIpDotted();

			portText = Controls.TextBox("Port", portText, "lobby_port", out commit, 8);
			if (commit) ApplyPort();
			GUILayout.BeginHorizontal();
			if (Controls.Button("Apply Port")) ApplyPort();
			if (Controls.Button("-100")) { port = (ushort)Math.Max(0, port - 100); portText = port.ToString(); }
			if (Controls.Button("-1")) { port = (ushort)Math.Max(0, port - 1); portText = port.ToString(); }
			if (Controls.Button("+1")) { port = (ushort)Math.Min(65535, port + 1); portText = port.ToString(); }
			if (Controls.Button("+100")) { port = (ushort)Math.Min(65535, port + 100); portText = port.ToString(); }
			GUILayout.EndHorizontal();

			gameIdText = Controls.TextBox("GameId", gameIdText, "lobby_gameid", out commit, 16);
			if (commit) ApplyGameId();
			if (Controls.Button("Apply GameId"))
				ApplyGameId();

			lobbyCodeText = Controls.TextBox("Lobby code", lobbyCodeText, "lobby_code", out commit, 12);
			if (commit) ApplyLobbyCode();
			if (Controls.Button("Code → GameId"))
				ApplyLobbyCode();

			GUILayout.Space(6);
			GUILayout.Label($"Current: {ipDotted}:{port}  gameId={gameId}  packed={ipPacked}");
			if (Controls.Button("Join"))
				TryJoin();
		}

		private void ApplyIpUint()
		{
			if (uint.TryParse(ipPackedText.Trim(), out uint v))
			{
				ipPacked = v;
				ipDotted = PackedToDotted(v);
				ipDottedText = ipDotted;
				Bane.notifications.Send("Lobby", $"IP uint → {ipDotted}", 3);
			}
			else
				Bane.notifications.Send("Lobby", "Invalid IP uint", 3);
		}

		private void ApplyIpDotted()
		{
			string s = ipDottedText.Trim();
			if (IPAddress.TryParse(s, out IPAddress addr))
			{
				byte[] b = addr.GetAddressBytes();
				if (b.Length == 4)
				{
					ipPacked = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
					ipDotted = s;
					ipPackedText = ipPacked.ToString();
					Bane.notifications.Send("Lobby", $"IP → {ipDotted} ({ipPacked})", 3);
					return;
				}
			}
			Bane.notifications.Send("Lobby", "Invalid dotted IP", 3);
		}

		private void ApplyPort()
		{
			if (ushort.TryParse(portText.Trim(), out ushort p))
			{
				port = p;
				Bane.notifications.Send("Lobby", $"Port → {port}", 2);
			}
			else
				Bane.notifications.Send("Lobby", "Invalid port", 3);
		}

		private void ApplyGameId()
		{
			if (int.TryParse(gameIdText.Trim(), out int id))
			{
				gameId = id;
				Bane.notifications.Send("Lobby", $"GameId → {gameId}", 2);
			}
			else
				Bane.notifications.Send("Lobby", "Invalid GameId", 3);
		}

		private void ApplyLobbyCode()
		{
			string s = (lobbyCodeText ?? "").Trim().ToUpperInvariant();
			try
			{
				gameId = GameCode.GameNameToInt(s);
				gameIdText = gameId.ToString();
				Bane.notifications.Send("Lobby", $"Code {s} → {gameId}", 3);
			}
			catch
			{
				Bane.notifications.Send("Lobby", "Invalid lobby code", 3);
			}
		}

		private void TryJoin()
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Lobby", "AmongUsClient missing", 3);
				return;
			}

			if (ipPacked == 0 && (ipDotted == "0.0.0.0" || string.IsNullOrEmpty(ipDotted)))
			{
				Bane.notifications.Send("Lobby", "Set an IP first", 3);
				return;
			}

			if (string.IsNullOrEmpty(ipDotted) || ipDotted == "0.0.0.0")
				ipDotted = PackedToDotted(ipPacked);

			Bane.Log.LogInfo($"Lobby join: {ipDotted}:{port} gameId={gameId}");

			try
			{
				AmongUsClient.Instance.SetEndpoint(ipDotted, port, false);
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning($"SetEndpoint failed: {e.Message}");
				try
				{
					AmongUsClient.Instance.SetEndpoint(ipDotted, port, true);
				}
				catch (Exception e2)
				{
					Bane.notifications.Send("Lobby", $"SetEndpoint failed: {e2.Message}", 5);
					return;
				}
			}

			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, false));
				Bane.notifications.Send("Lobby", $"Joining {GameCode.IntToGameName(gameId)} @ {ipDotted}:{port}", 5);
			}
			catch (Exception)
			{
				try
				{
					AmongUsClient.Instance.StartCoroutine(
						AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, true));
					Bane.notifications.Send("Lobby", $"Joining (alt) {gameId} @ {ipDotted}:{port}", 5);
				}
				catch (Exception e2)
				{
					Bane.Log.LogError($"Join failed: {e2}");
					Bane.notifications.Send("Lobby", $"Join failed: {e2.Message}", 5);
				}
			}
		}

		private static string PackedToDotted(uint packed)
		{
			return string.Format("{0}.{1}.{2}.{3}",
				(packed >> 24) & 0xFF,
				(packed >> 16) & 0xFF,
				(packed >> 8) & 0xFF,
				packed & 0xFF);
		}
	}
}
