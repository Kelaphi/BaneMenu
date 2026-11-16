using AmongUs.Data;
using BaneMenu.modules.misc;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using System.Collections;
using UnityEngine;

namespace BaneMenu.modules.troll
{
	/// <summary>
	/// When the host starts the lobby countdown, leave and rejoin the same code
	/// so the start timer resets. While out, randomize name/color/cosmetics.
	/// Retries join if it fails or if we get kicked right after a "successful" join.
	/// </summary>
	internal class AntiStartGame : Module
	{
		public AntiStartGame() : base("AntiStartGame") { }

		private static AntiStartGame Instance => ModuleManager.antiStartGame;

		private static bool isRejoining = false;
		private static int savedGameId = 0;
		private static readonly System.Random rnd = new System.Random();

		private const int MAX_JOIN_ATTEMPTS = 5;
		private const float JOIN_CONFIRM_TIMEOUT = 8f;
		private const float STABLE_LOBBY_SECONDS = 1.5f;
		private const float POST_SUCCESS_WATCH = 3f;

		private static void TryTrigger(string reason)
		{
			if (!Instance.Enabled) return;
			if (isRejoining) return;

			if (AmongUsClient.Instance == null) return;
			if (Utilities.IsHostEffective()) return;
			if (AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame) return;
			if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay) return;
			if (LobbyBehaviour.Instance == null) return;

			int gameId = AmongUsClient.Instance.GameId;
			if (gameId == 0 || gameId == -1) return;

			string code = "?";
			try { code = GameCode.IntToGameName(gameId); } catch { }

			savedGameId = gameId;
			isRejoining = true;

			Bane.Log.LogInfo($"AntiStartGame: triggered ({reason}), gameId={gameId} code={code}");
			Bane.notifications.Send("Anti Start Game", "Host tried to start the game but failed!", 5);

			Bane.modules.StartCoroutine(LeaveAndRejoin(gameId, code).WrapToIl2Cpp());
		}

		private static void RandomizeIdentity()
		{
			try
			{
				if (HatManager.Instance == null)
				{
					Bane.Log.LogWarning("AntiStartGame: HatManager not ready, skip disguise");
					return;
				}

				Utilities.RandomizePlayer(false);

				byte color = (byte)rnd.Next(0, 18);
				try
				{
					if (DataManager.Player != null && DataManager.Player.Customization != null)
						DataManager.Player.Customization.Color = color;
				}
				catch (System.Exception e)
				{
					Bane.Log.LogWarning($"AntiStartGame color set failed: {e.Message}");
				}

				Bane.Log.LogInfo($"AntiStartGame: randomized identity (color={color})");
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"AntiStartGame RandomizeIdentity failed: {e.Message}");
			}
		}

		/// <summary>Strict: must have LobbyBehaviour AND Joined state.</summary>
		private static bool IsFullyInLobby()
		{
			if (AmongUsClient.Instance == null) return false;
			if (LobbyBehaviour.Instance == null) return false;
			return AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined;
		}

		private static bool IsDisconnected()
		{
			if (AmongUsClient.Instance == null) return true;
			var state = AmongUsClient.Instance.GameState;
			return state == InnerNetClient.GameStates.NotJoined ||
			       state == InnerNetClient.GameStates.Ended;
		}

		/// <summary>
		/// Wait until fully in lobby for STABLE_LOBBY_SECONDS continuously,
		/// or until timeout / clear disconnect.
		/// </summary>
		private static IEnumerator WaitForStableLobby(float timeout)
		{
			float waited = 0f;
			float stable = 0f;

			while (waited < timeout)
			{
				yield return new WaitForSecondsRealtime(0.1f);
				waited += 0.1f;

				if (IsFullyInLobby())
				{
					stable += 0.1f;
					if (stable >= STABLE_LOBBY_SECONDS)
						yield break;
				}
				else
				{
					stable = 0f;
					// After a short grace, if clearly disconnected, stop waiting
					if (waited > 2f && IsDisconnected())
						yield break;
				}
			}
		}

		/// <summary>
		/// After we think we're in, watch for an immediate kick/disconnect.
		/// Returns true if still in lobby after the watch window.
		/// </summary>
		private static IEnumerator ConfirmStayInLobby(float watchSeconds)
		{
			float waited = 0f;
			while (waited < watchSeconds)
			{
				yield return new WaitForSecondsRealtime(0.15f);
				waited += 0.15f;

				if (!IsFullyInLobby())
					yield break; // dropped - caller treats as failure
			}
		}

		private static IEnumerator EnsureDisconnected(float timeout)
		{
			if (IsDisconnected())
				yield break;

			try { AmongUsClient.Instance?.ExitGame(DisconnectReasons.ExitGame); }
			catch { }

			float w = 0f;
			while (w < timeout)
			{
				yield return new WaitForSecondsRealtime(0.05f);
				w += 0.05f;
				if (IsDisconnected())
					yield break;
			}
		}

		private static bool TryStartJoin(int gameId)
		{
			if (AmongUsClient.Instance == null) return false;
			if (!IsDisconnected()) return false; // never stack joins while still "in"

			// Prefer AuraMenu quick rejoin (direct IP/port/token)
			if (QuickRejoin.LastGameId == 0)
				QuickRejoin.LastGameId = gameId;
			try
			{
				if (QuickRejoin.HasLobbyData)
				{
					QuickRejoin.RejoinQuick();
					Bane.Log.LogInfo("AntiStartGame: QuickRejoin.RejoinQuick");
					return true;
				}
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"QuickRejoin failed: {e.Message}");
			}

			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, true));
				Bane.Log.LogInfo("AntiStartGame: CoJoinOnlineGameFromCode(true)");
				return true;
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"CoJoin(true) failed: {e.Message}");
			}

			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, false));
				Bane.Log.LogInfo("AntiStartGame: CoJoinOnlineGameFromCode(false)");
				return true;
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"CoJoin(false) failed: {e.Message}");
			}

			return false;
		}

		private static IEnumerator LeaveAndRejoin(int gameId, string code)
		{
			// Capture before leave so quick rejoin has endpoint data
			if (AmongUsClient.Instance != null)
				QuickRejoin.CaptureFromClient(AmongUsClient.Instance);
			if (QuickRejoin.LastGameId == 0)
				QuickRejoin.LastGameId = gameId;

			// Leave current lobby
			yield return EnsureDisconnected(5f);

			if (!IsDisconnected())
			{
				Bane.Log.LogWarning("AntiStartGame: still not disconnected after ExitGame");
				isRejoining = false;
				yield break;
			}

			// Disguise once while out
			RandomizeIdentity();
			// Aura-style fixed 0.5s delay before quick rejoin
			yield return new WaitForSecondsRealtime(QuickRejoin.ForcedDelay);

			bool success = false;

			for (int attempt = 1; attempt <= MAX_JOIN_ATTEMPTS; attempt++)
			{
				if (AmongUsClient.Instance == null)
				{
					Bane.Log.LogWarning($"AntiStartGame: client null on attempt {attempt}");
					yield return new WaitForSecondsRealtime(0.5f);
					continue;
				}

				// Already stably in from a previous overlapping join?
				if (IsFullyInLobby())
				{
					yield return ConfirmStayInLobby(POST_SUCCESS_WATCH);
					if (IsFullyInLobby())
					{
						success = true;
						Bane.Log.LogInfo($"AntiStartGame: already in lobby (stable) on attempt {attempt}");
						break;
					}
					// Dropped during watch - fall through to retry
					Bane.Log.LogWarning($"AntiStartGame: was in lobby but dropped - retrying ({attempt})");
				}

				// Must be disconnected before starting another join
				if (!IsDisconnected())
				{
					Bane.Log.LogInfo($"AntiStartGame: forcing disconnect before attempt {attempt}");
					yield return EnsureDisconnected(3f);
				}

				if (attempt > 1)
				{
					Bane.Log.LogInfo($"AntiStartGame: join retry {attempt}/{MAX_JOIN_ATTEMPTS}");
					yield return new WaitForSecondsRealtime(0.4f);
				}

				bool started = TryStartJoin(gameId);
				if (!started)
				{
					Bane.Log.LogWarning($"AntiStartGame: could not start join on attempt {attempt}");
					yield return new WaitForSecondsRealtime(0.5f);
					continue;
				}

				// Wait until lobby is stable (not just a flicker of Joined)
				yield return WaitForStableLobby(JOIN_CONFIRM_TIMEOUT);

				if (!IsFullyInLobby())
				{
					Bane.Log.LogWarning($"AntiStartGame: attempt {attempt} failed to reach lobby (state={AmongUsClient.Instance?.GameState}, lobby={(LobbyBehaviour.Instance != null)})");
					// Clean up partial join before next try
					yield return EnsureDisconnected(3f);
					continue;
				}

				// Got in - watch for immediate kick (full room, host ban, etc.)
				Bane.Log.LogInfo($"AntiStartGame: reached lobby on attempt {attempt}, watching for drop...");
				yield return ConfirmStayInLobby(POST_SUCCESS_WATCH);

				if (IsFullyInLobby())
				{
					success = true;
					Bane.Log.LogInfo($"AntiStartGame: stable in lobby on attempt {attempt}");
					break;
				}

				Bane.Log.LogWarning($"AntiStartGame: attempt {attempt} joined then dropped - will retry");
				yield return EnsureDisconnected(3f);
			}

			if (!success)
			{
				Bane.Log.LogError($"AntiStartGame: all {MAX_JOIN_ATTEMPTS} join attempts failed for {code}");
				Bane.notifications.Send("Anti Start Game", "Failed to rejoin after several tries.", 5);
			}

			isRejoining = false;
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		class OnHandleRpc
		{
			static void Prefix(byte callId, MessageReader reader)
			{
				if (!Instance.Enabled) return;
				if (callId != (byte)RpcCalls.SetStartCounter) return;

				int pos = reader.Position;
				try
				{
					reader.ReadPackedInt32();
					sbyte counter = reader.ReadSByte();
					if (counter > 0)
						TryTrigger($"SetStartCounter={counter}");
				}
				catch
				{
					try
					{
						reader.Position = pos;
						sbyte counter = reader.ReadSByte();
						if (counter > 0)
							TryTrigger($"SetStartCounter(alt)={counter}");
					}
					catch { }
				}
				finally
				{
					reader.Position = pos;
				}
			}
		}

		private void OnGameStart()
		{
			TryTrigger("OnGameStart");
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			isRejoining = false;
			EventCoordinator.OnGameStart += OnGameStart;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnGameStart -= OnGameStart;
			isRejoining = false;
			savedGameId = 0;
		}
	}
}
