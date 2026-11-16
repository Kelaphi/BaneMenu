using HarmonyLib;

namespace BaneMenu.modules.protections
{
	internal class BlockUnauthorizedUpdates : Module
	{
		// The CloseDoorsOfType and UpdateSystem RPCs should only ever be sent to the host
		// It is possible for a non-host to send system updates to anyone they want
		// and cause a desync between our game state and the actual game state
		public BlockUnauthorizedUpdates() : base("BlockUnauthorizedUpdates")
		{
			/* default off until config */ base.Enabled = false;
		}

		private static BlockUnauthorizedUpdates Instance
		{
			get { return ModuleManager.blockUnauthorizedUpdates; }
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
		class OnShipStatusRPC
		{
			static bool Prefix(byte callId)
			{
				if(!Instance.Enabled) return true;

				if(!Utilities.IsHostEffective() && (callId == (byte)RpcCalls.UpdateSystem || callId == (byte)RpcCalls.CloseDoorsOfType))
				{
					return false;
				}

				return true;
			}
		}
	}
}