using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System;
using UnityEngine;

namespace BaneMenu.modules.host
{
	/// <summary>
	/// Creates NEW lobbies with a custom MaxPlayers at HostGame time (not mid-lobby option edits).
	/// Also has an experimental "NaN" create path for testing server handling.
	/// </summary>
	internal class SmallLobby : Module
	{
		public SmallLobby() : base("SmallLobby")
		{
			// Not a persistent toggle feature - buttons drive creates.
			// Enabled flag used as "inject max players on next HostGame".
		}

		/// <summary>MaxPlayers injected into the next HostGame options. -1 = no inject. -999 = NaN experiment.</summary>
		public static int PendingMaxPlayers = -1;

		public static bool PendingNaN = false;

		private static SmallLobby Instance => ModuleManager.smallLobby;

		public static void CreateLobby(int maxPlayers)
		{
			PendingNaN = false;
			PendingMaxPlayers = maxPlayers;
			Instance.Enabled = true;

			try
			{
				// Prefer GameHostOptions (what Create Game uses); fall back to current
				IGameOptions hostOpts = GameOptionsManager.Instance?.GameHostOptions
					?? GameOptionsManager.Instance?.CurrentGameOptions;

				if (hostOpts == null)
				{
					Bane.notifications.Send("Small Lobby", "No game options available. Open Create Game once first.", 6);
					return;
				}

				hostOpts.SetInt(Int32OptionNames.MaxPlayers, maxPlayers);

				// Also stomp CurrentGameOptions so UI stays consistent
				if (GameOptionsManager.Instance.CurrentGameOptions != null)
					GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.MaxPlayers, maxPlayers);

				Bane.Log.LogInfo($"SmallLobby: requesting HostGame with MaxPlayers={maxPlayers}");
				Bane.notifications.Send("Small Lobby", $"Creating lobby with max {maxPlayers} players…", 4);

				// Among Us host entry points vary; try the common ones
				if (!TryHostGame(hostOpts))
				{
					Bane.notifications.Send("Small Lobby", "Could not call HostGame API - use Create Game after enabling inject (check log).", 8);
				}
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"SmallLobby.CreateLobby failed: {e}");
				Bane.notifications.Send("Small Lobby", $"Create failed: {e.Message}", 8);
			}
		}

		public static void CreateNaNLobby()
		{
			PendingNaN = true;
			PendingMaxPlayers = -999;
			Instance.Enabled = true;

			try
			{
				IGameOptions hostOpts = GameOptionsManager.Instance?.GameHostOptions
					?? GameOptionsManager.Instance?.CurrentGameOptions;

				if (hostOpts == null)
				{
					Bane.notifications.Send("NaN Lobby", "No game options available. Open Create Game once first.", 6);
					return;
				}

				// IEEE NaN cannot be stored in int - C# rejects (int)float.NaN at compile time.
				// Runtime equivalent of that cast is 0; PendingNaN marks the experiment for logging / min-player logic.
				int nanAsInt = 0;
				Bane.Log.LogInfo($"SmallLobby: NaN experiment - using MaxPlayers={nanAsInt} (int has no NaN), PendingNaN=true");

				hostOpts.SetInt(Int32OptionNames.MaxPlayers, nanAsInt);

				Bane.notifications.Send("NaN Lobby", "Creating lobby with NaN max players (experimental)…", 5);

				if (!TryHostGame(hostOpts))
				{
					Bane.notifications.Send("NaN Lobby", "HostGame API missing - enable inject and use vanilla Create Game.", 8);
				}
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"SmallLobby.CreateNaNLobby failed: {e}");
				Bane.notifications.Send("NaN Lobby", $"Create failed: {e.Message}", 8);
			}
		}

		private static bool TryHostGame(IGameOptions opts)
		{
			var client = AmongUsClient.Instance;
			if (client == null) return false;

			// Try known method names across AU versions via reflection so we don't hard-break compile
			string[] names = { "HostGame", "CoHostGame", "CreateGame", "CoCreateGame", "StartGameHost" };
			foreach (string name in names)
			{
				var method = client.GetType().GetMethod(name, new Type[] { typeof(IGameOptions) })
					?? client.GetType().GetMethod(name, new Type[] { typeof(IGameOptions), typeof(bool) });

				if (method == null) continue;

				try
				{
					var parameters = method.GetParameters();
					object result;
					if (parameters.Length == 1)
						result = method.Invoke(client, new object[] { opts });
					else
						result = method.Invoke(client, new object[] { opts, false });

					// If it returns a coroutine (Il2Cpp), try to start it
					if (result is Il2CppSystem.Collections.IEnumerator enumerator)
						client.StartCoroutine(enumerator);
					else if (result is System.Collections.IEnumerator sysEnum)
						client.StartCoroutine(sysEnum as Il2CppSystem.Collections.IEnumerator);

					Bane.Log.LogInfo($"SmallLobby: invoked AmongUsClient.{name}");
					return true;
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning($"SmallLobby: {name} invoke failed: {e.Message}");
				}
			}

			return false;
		}


	}
}
