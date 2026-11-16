using HarmonyLib;

namespace BaneMenu.modules.protections
{
	internal class BlockServerTeleports : Module
	{
		public BlockServerTeleports() : base("BlockServerTeleports")
		{
			/* default off until config */ base.Enabled = false;
		}

		private static BlockServerTeleports Instance
		{
			get { return ModuleManager.blockServerTeleports; }
		}

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
		class OnRpc
		{
			static bool Prefix(CustomNetworkTransform __instance, byte callId)
			{
				if(!Instance.Enabled || callId != (byte)RpcCalls.SnapTo || __instance.myPlayer != PlayerControl.LocalPlayer) return true;

				Bane.Log.LogMessage($"Received SnapTo RPC for our player, since block server teleports is enabled we will disregard the RPC");
				return false;
			}
		}
	}
}