using BaneMenu.network;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;

namespace BaneMenu.modules.host
{
	internal class DisableGameEnd : Module
	{
		public DisableGameEnd() : base("DisableGameEnd") { }

		private static DisableGameEnd Instance => ModuleManager.disableGameEnd;

		private bool ShouldDespawnGameManager =>
			AmongUsClient.Instance != null
			&& !AmongUsClient.Instance.AmHost
			&& AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;

		[HarmonyPatch(typeof(GameManager), nameof(GameManager.RpcEndGame))]
		class OnEndGame
		{
			static bool Prefix() => !Instance.Enabled;
		}

		private void OnGameLoad()
		{
			if(!ShouldDespawnGameManager) return;
			if(GameManager.Instance == null) return;

			Bane.Log.LogMessage("Game has fully loaded, despawning GameManager");
			GameManager.Instance.Despawn();
		}

		private void OnNetObjectSpawn(InnerNetObject netObject)
		{
			if(netObject.SpawnId != (uint)SpawnType.GameManager || !ShouldDespawnGameManager) return;

			Bane.Log.LogMessage("A new instance of GameManager was spawned, despawning...");
			netObject.Despawn();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnGameLoad += OnGameLoad;
			EventCoordinator.OnNetObjectSpawn += OnNetObjectSpawn;

			if(AmongUsClient.Instance != null && ShouldDespawnGameManager && GameManager.Instance != null)
			{
				Bane.Log.LogMessage("NoGameEnd was enabled in a Local lobby as non-host, despawning GameManager");
				GameManager.Instance.Despawn();
			}
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnGameLoad -= OnGameLoad;
			EventCoordinator.OnNetObjectSpawn -= OnNetObjectSpawn;
		}
	}
}
