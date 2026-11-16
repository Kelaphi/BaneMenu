using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InnerNet;
using UnityEngine;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// Read-only client for Hydralum's public Firebase presence feed.
	/// Matches lobby players by PUID / friend code / (name+playerId) the same way
	/// Hydralum's PresenceTracker.IsHydralumUser does - without registering ourselves.
	/// </summary>
	internal static class HydralumPresence
	{
		const string FirebaseUrl = "https://hydralum-presence-default-rtdb.firebaseio.com/presence.json";
		static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

		static readonly object Lock = new object();
		static List<Peer> _roomPeers = new List<Peer>();
		static float _nextPoll;
		static int _pollInFlight;
		static string _lastRoom = "";

		public static bool Enabled = true;

		class Peer
		{
			public string Name = "";
			public int PlayerId = -1;
			public string FriendCode = "";
			public string Puid = "";
		}

		public static void Tick()
		{
			if (!Enabled) return;
			if (AmongUsClient.Instance == null) return;
			var state = AmongUsClient.Instance.GameState;
			if (state != InnerNet.InnerNetClient.GameStates.Joined &&
			    state != InnerNet.InnerNetClient.GameStates.Started)
				return;

			if (Time.unscaledTime < _nextPoll) return;
			_nextPoll = Time.unscaledTime + 3f;

			if (Interlocked.CompareExchange(ref _pollInFlight, 1, 0) != 0) return;

			string room = "";
			try
			{
				int gid = AmongUsClient.Instance.GameId;
				if (gid != 0) room = GameCode.IntToGameName(gid) ?? "";
			}
			catch { }

			_lastRoom = room ?? "";
			string roomCopy = _lastRoom;
			_ = Task.Run(async () =>
			{
				try { await PollAsync(roomCopy); }
				catch { }
				finally { Interlocked.Exchange(ref _pollInFlight, 0); }
			});
		}

		static async Task PollAsync(string roomCode)
		{
			using var resp = await Http.GetAsync(FirebaseUrl);
			if (!resp.IsSuccessStatusCode) return;
			string json = await resp.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(json) || json == "null") return;

			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var matched = new List<Peer>();

			using var doc = JsonDocument.Parse(json);
			foreach (var prop in doc.RootElement.EnumerateObject())
			{
				try
				{
					var el = prop.Value;
					if (el.ValueKind != JsonValueKind.Object) continue;

					long lastSeen = 0;
					if (el.TryGetProperty("last_seen", out var lsProp))
					{
						if (lsProp.TryGetInt64(out var ls64)) lastSeen = ls64;
						else if (lsProp.TryGetInt32(out var ls32)) lastSeen = ls32;
						else if (long.TryParse(lsProp.GetString(), out var lsP)) lastSeen = lsP;
					}
					long age = lastSeen > 0 ? Math.Max(0L, now - lastSeen) : 999999;
					if (age >= 60) continue;

					string room = el.TryGetProperty("room", out var rProp) ? rProp.GetString() ?? "" : "";
					if (string.IsNullOrEmpty(roomCode) ||
					    !string.Equals(room, roomCode, StringComparison.OrdinalIgnoreCase))
						continue;

					matched.Add(new Peer
					{
						Name = el.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "",
						PlayerId = el.TryGetProperty("p_id", out var idProp) && idProp.TryGetInt32(out var idVal) ? idVal : -1,
						FriendCode = el.TryGetProperty("friend_code", out var fcProp) ? fcProp.GetString() ?? "" : "",
						Puid = el.TryGetProperty("friend_puid", out var fpProp) ? fpProp.GetString() ?? "" : ""
					});
				}
				catch { }
			}

			lock (Lock)
			{
				_roomPeers = matched;
			}
		}

		public static bool IsHydralumUser(PlayerControl pc)
		{
			if (!Enabled || pc == null || pc.Data == null || pc.Data.Disconnected) return false;
			return IsHydralumUser(pc.Data);
		}

		public static bool IsHydralumUser(NetworkedPlayerInfo info)
		{
			if (!Enabled || info == null || info.Disconnected) return false;

			string targetName = info.PlayerName ?? "";
			int targetId = info.PlayerId;
			string targetFriendCode = info.FriendCode ?? "";
			string targetPuid = "";

			try
			{
				if (AmongUsClient.Instance != null)
				{
					var client = AmongUsClient.Instance.GetClientFromPlayerInfo(info);
					if (client != null)
					{
						if (!string.IsNullOrEmpty(client.ProductUserId)) targetPuid = client.ProductUserId;
						if (string.IsNullOrEmpty(targetFriendCode) && !string.IsNullOrEmpty(client.FriendCode))
							targetFriendCode = client.FriendCode;
					}
					if (string.IsNullOrEmpty(targetPuid) && info.Object != null)
					{
						var charClient = AmongUsClient.Instance.GetClientFromCharacter(info.Object);
						if (charClient != null && !string.IsNullOrEmpty(charClient.ProductUserId))
							targetPuid = charClient.ProductUserId;
					}
				}
			}
			catch { }

			List<Peer> peers;
			lock (Lock) { peers = _roomPeers; }
			return CheckPeerMatch(peers, targetName, targetId, targetFriendCode, targetPuid);
		}

		static bool CheckPeerMatch(List<Peer> peers, string targetName, int targetId, string targetFriendCode, string targetPuid)
		{
			if (peers == null || peers.Count == 0) return false;
			foreach (var peer in peers)
			{
				if (peer == null) continue;

				if (!string.IsNullOrWhiteSpace(peer.Puid) && !string.IsNullOrWhiteSpace(targetPuid) &&
				    string.Equals(peer.Puid.Trim(), targetPuid.Trim(), StringComparison.OrdinalIgnoreCase))
					return true;

				if (!string.IsNullOrWhiteSpace(peer.FriendCode) && !string.IsNullOrWhiteSpace(targetFriendCode) &&
				    string.Equals(peer.FriendCode.Trim(), targetFriendCode.Trim(), StringComparison.OrdinalIgnoreCase))
					return true;

				// Strict fallback: name AND playerId together
				if (!string.IsNullOrWhiteSpace(peer.Name) && !string.IsNullOrWhiteSpace(targetName) &&
				    peer.PlayerId >= 0 && targetId >= 0 &&
				    peer.PlayerId == targetId &&
				    string.Equals(peer.Name.Trim(), targetName.Trim(), StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}
	}
}
