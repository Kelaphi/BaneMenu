using BaneMenu.modules.misc;
using BaneMenu.network;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.modules.troll
{
	/// <summary>
	/// Lobby: VoteKick → leave → rejoin until target(s) disconnect.
	/// Kick-all votes EVERY remaining player each round, then one leave/rejoin.
	/// </summary>
	internal class LobbyVoteKickLoop : Module
	{
		public LobbyVoteKickLoop() : base("Lobby VoteKick Loop") { }

		static bool running;

		const int MAX_ROUNDS = 15;
		const float POST_JOIN_WAIT = 0.55f;
		const float POST_VOTE_WAIT = 0.12f;

		public static bool IsLobby()
		{
			if (AmongUsClient.Instance == null) return false;
			if (AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Joined) return false;
			if (ShipStatus.Instance != null) return false;
			return true;
		}

		public static bool IsRunning => running;

		public static void KickTarget(PlayerControl target)
		{
			if (target == null)
			{
				Bane.notifications.Send("Lobby VoteKick", "No target.", 3);
				return;
			}
			if (!IsLobby())
			{
				Bane.notifications.Send("Lobby VoteKick", "Only works in the lobby (before game start).", 4);
				return;
			}
			if (running)
			{
				Bane.notifications.Send("Lobby VoteKick", "Already running.", 3);
				return;
			}
			if (target == PlayerControl.LocalPlayer)
			{
				Bane.notifications.Send("Lobby VoteKick", "Can't target yourself.", 3);
				return;
			}

			string name = target.Data != null ? target.Data.PlayerName : null;
			if (string.IsNullOrEmpty(name))
			{
				Bane.notifications.Send("Lobby VoteKick", "Target has no name.", 3);
				return;
			}

			int gameId = AmongUsClient.Instance.GameId;
			if (gameId == 0 || gameId == -1)
			{
				Bane.notifications.Send("Lobby VoteKick", "No game id.", 3);
				return;
			}

			running = true;
			Bane.notifications.Send("Lobby VoteKick", "Looping VoteKick on " + name + "…", 4);
			try
			{
				if (Bane.modules != null)
					Bane.modules.StartCoroutine(LoopSingle(name, gameId).WrapToIl2Cpp());
				else
					running = false;
			}
			catch (System.Exception e)
			{
				running = false;
				Bane.Log.LogWarning("LobbyVoteKickLoop start: " + e.Message);
			}
		}

		public static void KickHost()
		{
			if (!IsLobby())
			{
				Bane.notifications.Send("Kick Host", "Only works in the lobby.", 4);
				return;
			}
			if (AmongUsClient.Instance == null) return;
			int hostId = AmongUsClient.Instance.HostId;
			PlayerControl host = null;
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc == null) continue;
				if (pc.OwnerId == hostId)
				{
					host = pc;
					break;
				}
			}
			if (host == null)
			{
				Bane.notifications.Send("Kick Host", "Host player not found.", 3);
				return;
			}
			if (host == PlayerControl.LocalPlayer)
			{
				Bane.notifications.Send("Kick Host", "You are the host.", 3);
				return;
			}
			KickTarget(host);
		}

		public static void KickAllInLobby()
		{
			if (!IsLobby())
			{
				Bane.notifications.Send("Kick All", "Lobby VoteKick method only in lobby.", 4);
				return;
			}
			if (running)
			{
				Bane.notifications.Send("Kick All", "Already running.", 3);
				return;
			}

			int gameId = AmongUsClient.Instance.GameId;
			if (gameId == 0 || gameId == -1)
			{
				Bane.notifications.Send("Kick All", "No game id.", 3);
				return;
			}

			running = true;
			Bane.notifications.Send("Kick All", "VoteKick everyone each round, then leave/rejoin…", 5);
			try
			{
				if (Bane.modules != null)
					Bane.modules.StartCoroutine(LoopAllParallel(gameId).WrapToIl2Cpp());
				else
					running = false;
			}
			catch (System.Exception e)
			{
				running = false;
				Bane.Log.LogWarning("KickAll: " + e.Message);
			}
		}

static string NormName(string name)
		{
			if (name == null) return "";
			return name.Trim();
		}

		static PlayerControl FindByName(string name)
		{
			if (string.IsNullOrEmpty(name)) return null;
			string want = NormName(name);
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc == null || pc.Data == null) continue;
				if (pc.Data.Disconnected) continue;
				if (NormName(pc.Data.PlayerName) == want) return pc;
			}
			return null;
		}

		/// <summary>
		/// After rejoin, AllPlayerControls is often only us for a moment - wait for roster to fill.
		/// </summary>
		static IEnumerator WaitForRoster(float minWait = 0.4f, float maxWait = 3f)
		{
			yield return new WaitForSecondsRealtime(minWait);
			float t = minWait;
			int lastCount = 0;
			float stable = 0f;
			while (t < maxWait)
			{
				int count = 0;
				if (PlayerControl.AllPlayerControls != null)
				{
					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
						count++;
					}
				}
				if (count > 1 && count == lastCount)
				{
					stable += 0.25f;
					if (stable >= 0.75f) yield break;
				}
				else
				{
					stable = 0f;
				}
				lastCount = count;
				yield return new WaitForSecondsRealtime(0.25f);
				t += 0.25f;
			}
		}

		static int CountSnapshotPresent(List<string> snapshot)
		{
			int n = 0;
			foreach (string name in snapshot)
			{
				if (FindByName(name) != null) n++;
			}
			return n;
		}


		static List<PlayerControl> CollectTargets()
		{
			var list = new List<PlayerControl>();
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc == null || pc == PlayerControl.LocalPlayer) continue;
				if (pc.Data == null || pc.Data.Disconnected) continue;
				if (Whitelist.ShouldSkip(pc)) continue;
				list.Add(pc);
			}
			return list;
		}

		static void VoteKickOnce(PlayerControl target)
		{
			if (target == null || AmongUsClient.Instance == null) return;
			if (VoteBanSystem.Instance == null)
			{
				Bane.Log.LogWarning("LobbyVoteKickLoop: no VoteBanSystem");
				return;
			}
			int src = AmongUsClient.Instance.ClientId;
			int tgt = target.OwnerId;
			try
			{
				BatchedMessage batch = new BatchedMessage();
				batch.QueueAddVote(src, tgt);
				batch.FinishBatch();
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("VoteKickOnce: " + e.Message);
			}
		}

		/// <summary>One vote per remaining player in a single batch when possible.</summary>
		static void VoteKickAllOnce()
		{
			if (AmongUsClient.Instance == null || VoteBanSystem.Instance == null) return;
			int src = AmongUsClient.Instance.ClientId;
			try
			{
				BatchedMessage batch = new BatchedMessage();
				int n = 0;
				foreach (PlayerControl pc in CollectTargets())
				{
					batch.QueueAddVote(src, pc.OwnerId);
					n++;
				}
				if (n > 0)
					batch.FinishBatch();
				Bane.Log.LogInfo("LobbyVoteKickLoop: voted " + n + " players this round");
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("VoteKickAllOnce: " + e.Message);
			}
		}

		static bool IsDisconnected()
		{
			if (AmongUsClient.Instance == null) return true;
			return AmongUsClient.Instance.GameState == InnerNetClient.GameStates.NotJoined;
		}

		static bool IsFullyInLobby()
		{
			if (AmongUsClient.Instance == null) return false;
			return AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined;
		}

		static IEnumerator EnsureDisconnected(float timeout)
		{
			float t = 0f;
			try { AmongUsClient.Instance?.ExitGame(DisconnectReasons.ExitGame); }
			catch { }
			while (t < timeout && !IsDisconnected())
			{
				yield return new WaitForSecondsRealtime(0.15f);
				t += 0.15f;
			}
		}

		static bool TryJoin(int gameId)
		{
			if (AmongUsClient.Instance == null) return false;
			if (QuickRejoin.LastGameId == 0)
				QuickRejoin.LastGameId = gameId;
			try
			{
				if (QuickRejoin.HasLobbyData)
				{
					QuickRejoin.RejoinQuick();
					return true;
				}
			}
			catch { }
			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, true));
				return true;
			}
			catch { }
			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, false));
				return true;
			}
			catch { }
			return false;
		}

		static IEnumerator Rejoin(int gameId)
		{
			// Aura: capture, wait delay (votes process), then CoJoinOnlineGameDirect - no ExitGame first
			if (AmongUsClient.Instance != null)
				QuickRejoin.CaptureFromClient(AmongUsClient.Instance);
			if (QuickRejoin.LastGameId == 0)
				QuickRejoin.LastGameId = gameId;

			yield return new WaitForSecondsRealtime(QuickRejoin.ForcedDelay);

			bool joined = false;
			for (int j = 0; j < 4; j++)
			{
				if (!TryJoin(gameId))
				{
					yield return new WaitForSecondsRealtime(0.5f);
					continue;
				}
				float w = 0f;
				while (w < 10f)
				{
					yield return new WaitForSecondsRealtime(0.2f);
					w += 0.2f;
					if (IsFullyInLobby())
					{
						joined = true;
						break;
					}
				}
				if (joined) break;
				yield return EnsureDisconnected(2f);
			}

			if (joined)
			{
				yield return new WaitForSecondsRealtime(POST_JOIN_WAIT);
				// Brief roster wait only - single-target needs speed
				yield return WaitForRoster(0.35f, 2.5f);
			}
		}

		static IEnumerator LoopSingle(string targetName, int gameId)
		{
			try
			{
				for (int round = 1; round <= MAX_ROUNDS; round++)
				{
					if (!IsFullyInLobby())
					{
						yield return Rejoin(gameId);
						if (!IsFullyInLobby())
						{
							Bane.notifications.Send("Lobby VoteKick", "Lost lobby - stopped.", 4);
							yield break;
						}
					}

					PlayerControl target = FindByName(targetName);
					if (target == null)
					{
						Bane.notifications.Send("Lobby VoteKick", targetName + " is gone.", 4);
						yield break;
					}

					VoteKickOnce(target);
					// Wait briefly - if they already DC'd, skip leave/rejoin
					yield return new WaitForSecondsRealtime(0.25f);
					if (FindByName(targetName) == null)
					{
						Bane.notifications.Send("Lobby VoteKick", targetName + " disconnected (no leave needed).", 4);
						yield break;
					}

					yield return Rejoin(gameId);

					if (!IsFullyInLobby())
					{
						Bane.notifications.Send("Lobby VoteKick", "Rejoin failed - stopped.", 4);
						yield break;
					}

					if (FindByName(targetName) == null)
					{
						Bane.notifications.Send("Lobby VoteKick", targetName + " disconnected.", 4);
						yield break;
					}
				}
				Bane.notifications.Send("Lobby VoteKick", "Max rounds - " + targetName + " still there.", 4);
			}
			finally
			{
				if (!IsFullyInLobby() && gameId != 0 && gameId != -1)
				{
					try
					{
						if (Bane.modules != null)
							Bane.modules.StartCoroutine(CleanupRejoin(gameId).WrapToIl2Cpp());
					}
					catch { }
				}
				running = false;
			}
		}

		/// <summary>
		/// Snapshot names at start. Each round votes every remaining snapshot target,
		/// then leave/rejoin once. Prunes snapshot as people leave. Stops on no progress.
		/// </summary>
		static IEnumerator LoopAllParallel(int gameId)
		{
			var snapshot = new List<string>();
			try
			{
				foreach (PlayerControl pc in CollectTargets())
				{
					if (pc.Data != null && !string.IsNullOrEmpty(pc.Data.PlayerName))
					{
						string n = NormName(pc.Data.PlayerName);
						if (!snapshot.Contains(n))
							snapshot.Add(n);
					}
				}

				if (snapshot.Count == 0)
				{
					Bane.notifications.Send("Kick All", "No targets at start.", 3);
					yield break;
				}

				Bane.Log.LogInfo("LobbyVoteKickLoop ALL snapshot count=" + snapshot.Count + ": " + string.Join(", ", snapshot));
				Bane.notifications.Send("Kick All", "Targets locked: " + snapshot.Count, 3);

				int noProgressRounds = 0;
				int lastPresent = snapshot.Count;

				for (int round = 1; round <= MAX_ROUNDS; round++)
				{
					if (!IsFullyInLobby())
					{
						yield return Rejoin(gameId);
						if (!IsFullyInLobby())
						{
							Bane.notifications.Send("Kick All", "Lost lobby - stopped.", 4);
							yield break;
						}
					}

					yield return WaitForRoster();

					// Prune snapshot + build still list
					var still = new List<PlayerControl>();
					var stillNames = new List<string>();
					foreach (string name in snapshot)
					{
						PlayerControl pc = FindByName(name);
						if (pc != null)
						{
							still.Add(pc);
							stillNames.Add(name);
						}
					}
					snapshot.Clear();
					snapshot.AddRange(stillNames);

					int present = still.Count;
					Bane.Log.LogInfo("LobbyVoteKickLoop: round " + round + " present " + present + " [" + string.Join(", ", stillNames) + "]");

					if (present == 0)
					{
						yield return new WaitForSecondsRealtime(2f);
						yield return WaitForRoster(1f, 4f);
						// re-scan original? snapshot already pruned - if empty, done
						if (CountSnapshotPresent(snapshot) == 0 && still.Count == 0)
						{
							// try rebuild from empty stillNames only
							Bane.notifications.Send("Kick All", "All original targets gone - stopping.", 4);
							yield break;
						}
					}

					if (present == 0)
					{
						Bane.notifications.Send("Kick All", "All original targets gone - stopping.", 4);
						yield break;
					}

					if (present >= lastPresent && round > 1)
						noProgressRounds++;
					else
						noProgressRounds = 0;
					lastPresent = present;

					if (noProgressRounds >= 2)
					{
						Bane.notifications.Send("Kick All", "No progress for 2 rounds - stopping. Left: " + present, 5);
						Bane.Log.LogWarning("LobbyVoteKickLoop: no progress, remaining=" + string.Join(", ", stillNames));
						yield break;
					}

					yield return VoteKickNamedOnceRoutine(still);
					yield return new WaitForSecondsRealtime(POST_VOTE_WAIT);
					yield return Rejoin(gameId);

					if (!IsFullyInLobby())
					{
						Bane.notifications.Send("Kick All", "Rejoin failed - cleanup will try again.", 4);
						yield break;
					}

					yield return WaitForRoster();
					int left = CountSnapshotPresent(snapshot);
					Bane.Log.LogInfo("LobbyVoteKickLoop: after rejoin present " + left);
					if (left == 0)
					{
						yield return new WaitForSecondsRealtime(2f);
						yield return WaitForRoster(1f, 4f);
						left = CountSnapshotPresent(snapshot);
						if (left == 0)
						{
							Bane.notifications.Send("Kick All", "All original targets gone - stopping.", 4);
							yield break;
						}
					}
				}
				Bane.notifications.Send("Kick All", "Max rounds - some may remain.", 4);
			}
			finally
			{
				if (!IsFullyInLobby() && gameId != 0 && gameId != -1)
				{
					Bane.Log.LogInfo("LobbyVoteKickLoop: cleanup rejoin");
					try
					{
						if (Bane.modules != null)
							Bane.modules.StartCoroutine(CleanupRejoin(gameId).WrapToIl2Cpp());
					}
					catch { }
				}
				running = false;
			}
		}

		static IEnumerator CleanupRejoin(int gameId)
		{
			yield return Rejoin(gameId);
			if (IsFullyInLobby())
				Bane.notifications.Send("Kick All", "Rejoined after finish.", 3);
			else
				Bane.notifications.Send("Kick All", "Could not rejoin after finish.", 4);
		}

		/// <summary>
		/// Separate AddVote packets; 2 votes per target so the leave registers harder.
		/// </summary>
		static IEnumerator VoteKickNamedOnceRoutine(List<PlayerControl> targets)
		{
			if (AmongUsClient.Instance == null) yield break;

			float waitSys = 0f;
			while (VoteBanSystem.Instance == null && waitSys < 5f)
			{
				yield return new WaitForSecondsRealtime(0.1f);
				waitSys += 0.1f;
			}
			if (VoteBanSystem.Instance == null)
			{
				Bane.Log.LogWarning("LobbyVoteKickLoop: VoteBanSystem missing - skip vote round");
				yield break;
			}

			int src = AmongUsClient.Instance.ClientId;
			int n = 0;
			foreach (PlayerControl pc in targets)
			{
				if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
				for (int v = 0; v < 2; v++)
				{
					try
					{
						BatchedMessage batch = new BatchedMessage();
						batch.QueueAddVote(src, pc.OwnerId);
						batch.FinishBatch();
						n++;
						Bane.Log.LogInfo("LobbyVoteKickLoop: vote -> " + pc.Data.PlayerName + " client=" + pc.OwnerId + " n=" + (v + 1));
					}
					catch (System.Exception e)
					{
						Bane.Log.LogWarning("vote " + pc.Data.PlayerName + ": " + e.Message);
					}
					yield return new WaitForSecondsRealtime(0.08f);
				}
			}
			Bane.Log.LogInfo("LobbyVoteKickLoop: sent " + n + " vote packets this round");
		}
	}
}
