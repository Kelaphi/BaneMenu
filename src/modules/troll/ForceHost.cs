using BaneMenu.modules.misc;
using BaneMenu.network;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BaneMenu.modules.troll
{
	/// <summary>
	/// Force Host - same flow as Hydralum/Malum ForceHost:
	/// VoteKick host (x3, 0.05s) → brief wait → ExitGame → 0.6s → CoJoin once → repeat until AmHost.
	/// Coroutine runs on Bane.modules so ExitGame does not kill the loop.
	/// </summary>
	internal class ForceHost : Module
	{
		public ForceHost() : base("Force Host") { }

		static bool running;
		static int runGeneration;
		static int hostsKicked;
		static int joinCycle;
		static int savedGameId;
		static string savedCode = "";

		// Hydralum defaults
		public static int VoteCount { get; set; } = 3;
		public static float VoteDelay { get; set; } = 0.05f;
		public static float ExitDelay { get; set; } = 0.6f;
		public static bool StopOnJoinFailure { get; set; } = false;

		public static bool IsRunning => running;
		public static int HostsKicked => hostsKicked;
		public static int JoinCycle => joinCycle;

		static bool Alive(int gen) => running && gen == runGeneration;

		public static bool IsInGameOrLobby()
		{
			try
			{
				if (AmongUsClient.Instance == null) return false;
				if ((int)AmongUsClient.Instance.GameState > 0) return true;
				string scene = SceneManager.GetActiveScene().name;
				return scene != "MainMenu" && scene != "MatchMaking";
			}
			catch { return false; }
		}

		public static void Start()
		{
			if (running)
			{
				Bane.notifications.Send("Force Host", "Already running.", 3);
				return;
			}
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Force Host", "Must be in a lobby or game.", 4);
				return;
			}
			if (AmongUsClient.Instance.AmHost)
			{
				Bane.notifications.Send("Force Host", "You are already the host.", 3);
				return;
			}

			int gameId = AmongUsClient.Instance.GameId;
			string roomCode = gameId != 0 ? GameCode.IntToGameName(gameId) : "";
			if (string.IsNullOrEmpty(roomCode) && gameId == 0)
			{
				Bane.notifications.Send("Force Host", "No game code found.", 4);
				return;
			}
			if (gameId == 0)
			{
				try { gameId = GameCode.GameNameToIntV2(roomCode); } catch { gameId = 0; }
			}
			if (gameId == 0)
			{
				Bane.notifications.Send("Force Host", "Invalid game code.", 4);
				return;
			}
			if (Bane.modules == null)
			{
				Bane.notifications.Send("Force Host", "ModuleManager missing.", 4);
				return;
			}

			savedGameId = gameId;
			savedCode = roomCode ?? GameCode.IntToGameName(gameId);
			hostsKicked = 0;
			joinCycle = 0;
			runGeneration++;
			int gen = runGeneration;
			running = true;

			Bane.notifications.Send("Force Host", "Started…", 3);
			Bane.Log.LogInfo($"ForceHost: start code={savedCode} id={gameId}");

			Bane.modules.StartCoroutine(ForceHostLoop(gen).WrapToIl2Cpp());
		}

		public static void Stop()
		{
			runGeneration++;
			running = false;
			Bane.notifications.Send("Force Host", "Stopped.", 3);
		}

		static IEnumerator ForceHostLoop(int gen)
		{
			while (Alive(gen))
			{
				if (AmongUsClient.Instance == null)
				{
					yield return new WaitForSeconds(ExitDelay);
					continue;
				}

				if (AmongUsClient.Instance.AmHost)
				{
					running = false;
					Bane.notifications.Send("Force Host", "Success - you are host.", 5);
					yield break;
				}

				// Not in lobby → AuraMenu quick rejoin (0.5s fixed)
				if (!IsInGameOrLobby())
				{
					if (QuickRejoin.LastGameId == 0 && savedGameId != 0)
						QuickRejoin.LastGameId = savedGameId;

					joinCycle++;
					Bane.Log.LogInfo($"ForceHost: quick rejoin cycle #{joinCycle}");
					yield return QuickRejoin.QueueRejoin(QuickRejoin.ForcedDelay, true);

					// Wait up to 5s for connection
					float wait = 0f;
					while (wait < 5f && Alive(gen)
						&& (AmongUsClient.Instance == null || (int)AmongUsClient.Instance.GameState == 0))
					{
						yield return null;
						wait += Time.deltaTime;
					}

					if (!Alive(gen)) yield break;

					if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
					{
						running = false;
						Bane.notifications.Send("Force Host", "Success - you are host.", 5);
						yield break;
					}

					if (AmongUsClient.Instance == null || (int)AmongUsClient.Instance.GameState == 0)
					{
						if (StopOnJoinFailure)
						{
							running = false;
							Bane.notifications.Send("Force Host", "Join failed - stopped.", 4);
							yield break;
						}
					}

					yield return new WaitForSeconds(0.5f);
					continue;
				}

				// In lobby - Hydralum method:
				// cycles 1–2: VoteKick EVERYONE (builds vote stacks), then leave/rejoin
				// cycle 3+: VoteKick host only until we become host
				if (VoteBanSystem.Instance != null && AmongUsClient.Instance != null)
				{
					int hostId = AmongUsClient.Instance.HostId;
					if (hostId == AmongUsClient.Instance.ClientId || AmongUsClient.Instance.AmHost)
					{
						running = false;
						Bane.notifications.Send("Force Host", "Success - you are host.", 5);
						yield break;
					}

					bool kickAllPhase = joinCycle < 2;
					if (kickAllPhase)
					{
						Bane.notifications.Send("Force Host", $"Pass {joinCycle + 1}/2: voting all…", 2);
						foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
						{
							if (!Alive(gen)) yield break;
							if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
							if (pc == PlayerControl.LocalPlayer) continue;
							int targetId = pc.OwnerId;
							try { VoteBanSystem.Instance.CmdAddVote(targetId); }
							catch
							{
								try
								{
									var batch = new BatchedMessage();
									batch.QueueAddVote(AmongUsClient.Instance.ClientId, targetId);
									batch.FinishBatch();
								}
								catch { }
							}
							yield return new WaitForSeconds(VoteDelay);
						}
					}
					else
					{
						for (int i = 0; i < VoteCount; i++)
						{
							if (!Alive(gen)) yield break;
							try { VoteBanSystem.Instance.CmdAddVote(hostId); }
							catch
							{
								try
								{
									var batch = new BatchedMessage();
									batch.QueueAddVote(AmongUsClient.Instance.ClientId, hostId);
									batch.FinishBatch();
								}
								catch { }
							}
							yield return new WaitForSeconds(VoteDelay);
						}
						hostsKicked++;
						Bane.notifications.Send("Force Host", $"Voted host (#{hostsKicked})", 2);
					}
				}

				yield return new WaitForSeconds(0.3f);
				if (!Alive(gen)) yield break;

				if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
				{
					running = false;
					Bane.notifications.Send("Force Host", "Success - you are host.", 5);
					yield break;
				}

				// Aura style: wait delay so votes process, then CoJoinOnlineGameDirect (no ExitGame)
				if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost && IsInGameOrLobby())
				{
					QuickRejoin.CaptureFromClient(AmongUsClient.Instance);
					yield return QuickRejoin.QueueRejoin(QuickRejoin.ForcedDelay, true);
				}
				else
				{
					yield return new WaitForSeconds(QuickRejoin.ForcedDelay);
				}
			}

			if (gen == runGeneration)
				running = false;
		}
	}
}
