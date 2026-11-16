using HarmonyLib;
using UnityEngine;

namespace BaneMenu.modules.host
{
	/// <summary>
	/// SickoMenu AlwaysAllowStart: lower GameStartManager.MinPlayers so the Start
	/// button enables even with fewer than 4 players in lobby.
	/// </summary>
	internal class AlwaysAllowStart : Module
	{
		public AlwaysAllowStart() : base("Always Allow Start Button") { }

		static AlwaysAllowStart Instance => ModuleManager.alwaysAllowStart;

		[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
		class GameStartUpdatePatch
		{
			static void Prefix(GameStartManager __instance)
			{
				if (!Instance.Enabled || __instance == null) return;
				try
				{
					if (AmongUsClient.Instance == null) return;
					if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Joined) return;
					int last = __instance.LastPlayerCount;
					__instance.MinPlayers = Mathf.Min(Mathf.Max(last, 1), 4);
				}
				catch { }
			}
		}
	}
}
