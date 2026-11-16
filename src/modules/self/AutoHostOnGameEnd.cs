using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BaneMenu.modules.self
{
	internal class AutoHostOnGameEnd : Module
	{
		public AutoHostOnGameEnd() : base("Auto Host On Game End") { }

		static AutoHostOnGameEnd Instance => ModuleManager.autoHostOnGameEnd;

		static bool isRejoining;
		static int savedGameId;
		static string savedCode = "";

		static GameObject runnerGo;
		static CoroutineHost runner;

		public void Tick()
		{
			if (!Enabled || AmongUsClient.Instance == null) return;
			if (isRejoining) return;

			int id = AmongUsClient.Instance.GameId;
			if (id == 0 || id == -1) return;
			if (AmongUsClient.Instance.GameState == InnerNetClient.GameStates.NotJoined) return;

			savedGameId = id;
			try { savedCode = GameCode.IntToGameName(id) ?? savedCode; }
			catch { }
		}

		static void EnsureRunner()
		{
			if (runner != null && runnerGo != null) return;
			runnerGo = new GameObject("BaneAutoHostRunner");
			UnityEngine.Object.DontDestroyOnLoad(runnerGo);
			runner = runnerGo.AddComponent<CoroutineHost>();
		}

		static void TryTrigger(string reason)
		{
			if (Instance == null || !Instance.Enabled) return;
			if (isRejoining) return;
			if (AmongUsClient.Instance == null) return;
			if (AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame) return;

			int gameId = savedGameId != 0 ? savedGameId : AmongUsClient.Instance.GameId;
			if (gameId == 0 || gameId == -1)
			{
				Bane.notifications.Send("Auto Host", "No saved room code.", 3);
				return;
			}

			string code = savedCode;
			try
			{
				if (string.IsNullOrEmpty(code))
					code = GameCode.IntToGameName(gameId);
			}
			catch { }

			isRejoining = true;
			Bane.Log.LogInfo($"AutoHostOnGameEnd: trigger ({reason}) code={code} id={gameId} scene={SceneManager.GetActiveScene().name}");
			Bane.notifications.Send("Auto Host", $"Force-leaving then rejoin {code}…", 4);

			try
			{
				EnsureRunner();
				runner.StartCoroutine(LeaveAndRejoin(gameId, code).WrapToIl2Cpp());
			}
			catch (Exception e)
			{
				isRejoining = false;
				Bane.Log.LogWarning("AutoHostOnGameEnd start failed: " + e.Message);
			}
		}

		static bool IsNotJoined()
		{
			if (AmongUsClient.Instance == null) return true;
			return AmongUsClient.Instance.GameState == InnerNetClient.GameStates.NotJoined;
		}

		static bool IsFullyInLobby()
		{
			if (AmongUsClient.Instance == null) return false;
			if (LobbyBehaviour.Instance == null) return false;
			return AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined;
		}

		static void InvokeMatching(object obj, string[] nameParts)
		{
			if (obj == null) return;
			Type t = obj.GetType();
			foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (m.GetParameters().Length > 1) continue;
				string n = m.Name;
				bool match = false;
				foreach (string p in nameParts)
				{
					if (n.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						match = true;
						break;
					}
				}
				if (!match) continue;
				try
				{
					object[] args = null;
					if (m.GetParameters().Length == 1)
					{
						var pt = m.GetParameters()[0].ParameterType;
						if (pt.IsEnum)
							args = new object[] { Enum.ToObject(pt, (int)DisconnectReasons.ExitGame) };
						else if (pt == typeof(bool))
							args = new object[] { true };
						else
							continue;
					}
					m.Invoke(obj, args);
					Bane.Log.LogInfo("AutoHostOnGameEnd: invoked " + t.Name + "." + n);
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning("Invoke " + n + ": " + e.Message);
				}
			}
		}

		static void AggressiveLeave()
		{
			string scene = SceneManager.GetActiveScene().name;
			Bane.Log.LogInfo("AutoHostOnGameEnd: AggressiveLeave scene=" + scene +
				" state=" + (AmongUsClient.Instance != null ? AmongUsClient.Instance.GameState.ToString() : "null"));

			// End game UI objects
			try
			{
				foreach (var eg in UnityEngine.Object.FindObjectsOfType<EndGameManager>())
				{
					InvokeMatching(eg, new[] { "Continue", "Exit", "Return", "MainMenu", "Quit", "Close", "Next" });
					try { UnityEngine.Object.Destroy(eg.gameObject); } catch { }
				}
			}
			catch (Exception e) { Bane.Log.LogWarning("EndGameManager leave: " + e.Message); }

			// AmongUsClient leave APIs
			try
			{
				var auc = AmongUsClient.Instance;
				if (auc != null)
				{
					InvokeMatching(auc, new[] { "ExitGame", "Disconnect", "FinishEndGame", "ReturnToMenu", "MainMenu" });
					try { auc.ExitGame(DisconnectReasons.ExitGame); }
					catch { }
				}
			}
			catch (Exception e) { Bane.Log.LogWarning("AUC leave: " + e.Message); }

			// Force single-load MainMenu by build index + name
			try
			{
				int count = SceneManager.sceneCountInBuildSettings;
				Bane.Log.LogInfo("AutoHostOnGameEnd: sceneCountInBuildSettings=" + count);
				for (int i = 0; i < count; i++)
				{
					string path = SceneUtility.GetScenePathByBuildIndex(i);
					Bane.Log.LogInfo("  scene[" + i + "]=" + path);
					if (path != null && path.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						Bane.Log.LogInfo("AutoHostOnGameEnd: LoadScene buildIndex " + i);
						SceneManager.LoadScene(i, LoadSceneMode.Single);
						return;
					}
				}
			}
			catch (Exception e) { Bane.Log.LogWarning("buildIndex load: " + e.Message); }

			try
			{
				SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
				Bane.Log.LogInfo("AutoHostOnGameEnd: LoadScene(MainMenu, Single)");
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning("LoadScene MainMenu: " + e.Message);
			}
		}

		static IEnumerator LeaveAndRejoin(int gameId, string code)
		{
			yield return new WaitForSecondsRealtime(0.5f);

			AggressiveLeave();
			yield return new WaitForSecondsRealtime(0.4f);

			// Second push if still stuck on end/game scene
			if (!IsNotJoined())
			{
				Bane.Log.LogInfo("AutoHostOnGameEnd: still not NotJoined - second AggressiveLeave");
				AggressiveLeave();
			}

			float w = 0f;
			while (w < 12f)
			{
				yield return new WaitForSecondsRealtime(0.2f);
				w += 0.2f;

				string scene = SceneManager.GetActiveScene().name ?? "";
				bool menu = false;
				try { menu = UnityEngine.Object.FindObjectOfType<MainMenuManager>() != null; } catch { }

				Bane.Log.LogInfo("AutoHostOnGameEnd: wait leave t=" + w.ToString("0.0") +
					" scene=" + scene + " notJoined=" + IsNotJoined() + " menu=" + menu);

				if (IsNotJoined() && (menu || w > 3f))
					break;

				if (w >= 3f && (int)w % 2 == 0)
					AggressiveLeave();
			}

			Bane.Log.LogInfo("AutoHostOnGameEnd: settle 1.5s before join");
			yield return new WaitForSecondsRealtime(1.5f);

			w = 0f;
			while (w < 8f && AmongUsClient.Instance == null)
			{
				yield return new WaitForSecondsRealtime(0.2f);
				w += 0.2f;
			}
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Auto Host", "AmongUsClient null after leave.", 5);
				isRejoining = false;
				yield break;
			}

			bool joined = false;
			for (int attempt = 1; attempt <= 5; attempt++)
			{
				Bane.Log.LogInfo("AutoHostOnGameEnd: join attempt " + attempt + " " + code);
				if (!TryStartJoin(gameId))
				{
					yield return new WaitForSecondsRealtime(0.5f);
					continue;
				}

				float waited = 0f;
				while (waited < 12f)
				{
					yield return new WaitForSecondsRealtime(0.2f);
					waited += 0.2f;
					if (IsFullyInLobby())
					{
						joined = true;
						break;
					}
				}
				if (joined) break;
				AggressiveLeave();
				yield return new WaitForSecondsRealtime(1f);
			}

			if (joined)
			{
				int cid = AmongUsClient.Instance.ClientId;
				bool host = AmongUsClient.Instance.AmHost;
				Bane.notifications.Send("Auto Host", "Rejoined " + code + " ClientId=" + cid + " AmHost=" + host, 6);
			}
			else
			{
				Bane.notifications.Send("Auto Host", "Leave/rejoin failed - check log.", 5);
			}

			isRejoining = false;
		}

		static bool TryStartJoin(int gameId)
		{
			if (AmongUsClient.Instance == null) return false;
			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, true));
				return true;
			}
			catch (Exception e) { Bane.Log.LogWarning("CoJoin true: " + e.Message); }
			try
			{
				AmongUsClient.Instance.StartCoroutine(
					AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, false));
				return true;
			}
			catch (Exception e) { Bane.Log.LogWarning("CoJoin false: " + e.Message); }
			return false;
		}

		[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
		static class PatchEndGameManager
		{
			static void Postfix() => TryTrigger("EndGameManager.Start");
		}
	}

	internal class CoroutineHost : MonoBehaviour
	{
	}
}
