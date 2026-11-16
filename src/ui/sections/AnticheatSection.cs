using BaneMenu.anticheat;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class AnticheatSection : Section
	{
		public AnticheatSection() : base("Anticheat") { }

		public override void Render()
		{
			Anticheat.Enabled = Controls.Toggle(Anticheat.Enabled, "Enable BaneMenu Anticheat");

			Anticheat.CheckSpoofedPlatforms = Controls.Toggle(Anticheat.CheckSpoofedPlatforms, "Flag Spoofed Platform Data");
			// Mod Menu Check lives under Protections → General (independent of AC on/off)

			GUILayout.Space(5);
			GUILayout.Label("RPCs that should be checked by the anticheat:");
			foreach((RpcCalls rpcCall, RpcCheck handler) in Anticheat.RpcHandlers)
			{
				handler.Enabled = Controls.Toggle(handler.Enabled, $"{rpcCall}");
			}

			GUILayout.Space(5);
			GUILayout.Label("When a cheater is detected:");
			Anticheat.sendNotification = Controls.Toggle(Anticheat.sendNotification, "Send notification");
			Anticheat.discardRpc = Controls.Toggle(Anticheat.discardRpc, "Discard RPC");

			{
				string[] punishNames = { "None", "Kick", "ErrorKick", "Ban" };
				int p = (int)Anticheat.punishment;
				if (p < 0) p = 0;
				if (p > 3) p = 3;
				p = Controls.Dropdown("Punish the player with", punishNames, p, "ac_punish_dd");
				Anticheat.punishment = (Anticheat.Punishments)p;
			}
		}
	}
}