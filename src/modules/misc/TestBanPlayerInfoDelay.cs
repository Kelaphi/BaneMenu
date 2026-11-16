using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// Disable NetworkManager on join; re-enable after delay.
	/// Coroutine on Bane.modules (survives NetworkManager disable).
	/// Keeps direct refs - inactive GOs are not destroyed.
	/// </summary>
	internal class TestBanPlayerInfoDelay : Module
	{
		public TestBanPlayerInfoDelay() : base("Test Ban Player Info Delay") { }

		public float DelaySeconds { get; set; } = 4f;

		static TestBanPlayerInfoDelay Instance => ModuleManager.testBanPlayerInfoDelay;

		static bool running;

		public static void Arm(string reason)
		{
			if (Instance == null || !Instance.Enabled) return;
			if (running) return;

			float d = Mathf.Max(0.5f, Instance.DelaySeconds);
			Bane.Log.LogInfo($"TestBanPlayerInfoDelay: ARM ({reason}) NetworkManager off {d:0.0}s");
			Bane.notifications.Send("Info Delay", $"NetworkManager off {d:0.0}s", 3);

			try
			{
				if (Bane.modules != null)
					Bane.modules.StartCoroutine(DisableThenRestore(d).WrapToIl2Cpp());
				else
					Bane.Log.LogWarning("TestBanPlayerInfoDelay: Bane.modules null");
			}
			catch (System.Exception e)
			{
				running = false;
				Bane.Log.LogWarning("TestBanPlayerInfoDelay start failed: " + e.Message);
			}
		}

		public void Tick() { }

		static IEnumerator DisableThenRestore(float seconds)
		{
			running = true;
			var disabledGos = new List<GameObject>();
			var disabledBehaviours = new List<Behaviour>();

			try
			{
				foreach (var tr in UnityEngine.Object.FindObjectsOfType<Transform>())
				{
					if (tr == null) continue;
					GameObject go = tr.gameObject;
					if (go == null || go.name != "NetworkManager") continue;
					if (!go.activeSelf) continue;
					Bane.Log.LogInfo("TestBanPlayerInfoDelay: disable GO NetworkManager");
					go.SetActive(false);
					disabledGos.Add(go);
				}

				foreach (var b in UnityEngine.Object.FindObjectsOfType<Behaviour>())
				{
					if (b == null) continue;
					string tn = b.GetType().Name;
					if (tn == null || tn.IndexOf("NetworkManager") < 0) continue;
					if (!b.enabled) continue;
					Bane.Log.LogInfo("TestBanPlayerInfoDelay: disable component " + tn);
					b.enabled = false;
					disabledBehaviours.Add(b);
				}

				if (disabledGos.Count == 0 && disabledBehaviours.Count == 0)
					Bane.Log.LogWarning("TestBanPlayerInfoDelay: nothing to disable");
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("TestBanPlayerInfoDelay disable: " + e.Message);
			}

			Bane.Log.LogInfo("TestBanPlayerInfoDelay: waiting " + seconds + "s");
			yield return new WaitForSecondsRealtime(seconds);

			int restored = 0;
			try
			{
				foreach (var go in disabledGos)
				{
					if (go == null)
					{
						Bane.Log.LogWarning("TestBanPlayerInfoDelay: GO ref became null");
						continue;
					}
					go.SetActive(true);
					restored++;
					Bane.Log.LogInfo("TestBanPlayerInfoDelay: re-enabled GO NetworkManager");
				}
				foreach (var b in disabledBehaviours)
				{
					if (b == null)
					{
						Bane.Log.LogWarning("TestBanPlayerInfoDelay: Behaviour ref became null");
						continue;
					}
					b.enabled = true;
					restored++;
					Bane.Log.LogInfo("TestBanPlayerInfoDelay: re-enabled " + b.GetType().Name);
				}
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("TestBanPlayerInfoDelay restore: " + e.Message);
			}

			Bane.notifications.Send("Info Delay", "NetworkManager re-enabled (" + restored + ")", 3);
			Bane.Log.LogInfo("TestBanPlayerInfoDelay: done restored=" + restored);
			running = false;
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoJoinOnlineGameFromCode))]
		static class PatchCoJoin
		{
			static void Prefix() => Arm("CoJoinOnlineGameFromCode");
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		static class PatchLobbyStart
		{
			static void Postfix()
			{
				if (!running)
					Arm("LobbyBehaviour.Start");
			}
		}
	}
}
