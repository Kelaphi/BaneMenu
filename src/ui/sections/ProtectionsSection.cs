using BaneMenu.modules;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class ProtectionsSection : Section
	{
		static readonly string[] SubNames = { "General", "Anticheat" };
		readonly Section anticheat = new AnticheatSection();
		int subTab;

		public ProtectionsSection() : base("Protections") { }

		public override void HandleSubsectionMove(int offset)
		{
			subTab = Mathf.Clamp(subTab + offset, 0, SubNames.Length - 1);
			BaneGui.ClosePopup();
			BaneGui.Unfocus();
		}

		public override void Render()
		{
			int prev = subTab;
			subTab = Controls.SubTabs(SubNames, subTab, "prot_subtabs");
			if (subTab != prev) { BaneGui.ClosePopup(); BaneGui.Unfocus(); }
			GUILayout.Space(4);
			if (subTab == 1) { anticheat.Render(); return; }

			// Independent of BaneMenu Anticheat enable
			GUILayout.Label("--- Detection ---");
			BaneMenu.anticheat.ModMenuCheck.Enabled = Controls.Toggle(
				BaneMenu.anticheat.ModMenuCheck.Enabled,
				"Mod Menu Check (custom RPC fingerprints)");
			GUILayout.Label("Flags known menu RPC signatures. Independent of AC.");
			if (BaneMenu.anticheat.ModMenuCheck.Enabled)
			{
				BaneMenu.anticheat.ModMenuCheck.DetectSicko = Controls.Toggle(
					BaneMenu.anticheat.ModMenuCheck.DetectSicko,
					"Detect Sicko users (RPC 164)");
				GUILayout.Label("SickoMenu handshake uses custom RPC 164.");
			}
			BaneMenu.modules.misc.HydralumPresence.Enabled = Controls.Toggle(
				BaneMenu.modules.misc.HydralumPresence.Enabled,
				"Detect Hydralum users (presence feed)");
			GUILayout.Label("Matches lobby players against Hydralum public presence.");
			GUILayout.Space(6);

			// Network
			ModuleManager.forceDtls.Enabled = Controls.Toggle(ModuleManager.forceDtls.Enabled, "Force enable DTLS to encrypt network data");

			ModuleManager.bypassDisconnectPenalty.Enabled = Controls.Toggle(ModuleManager.bypassDisconnectPenalty.Enabled, "Bypass disconnection penalties");
			ModuleManager.blockServerTeleports.Enabled = Controls.Toggle(ModuleManager.blockServerTeleports.Enabled, "Block position updates from server");
			ModuleManager.blockUnauthorizedUpdates.Enabled = Controls.Toggle(ModuleManager.blockUnauthorizedUpdates.Enabled, "Block unauthorized system updates");
			ModuleManager.bypassShapeshiftRatelimits.Enabled = Controls.Toggle(ModuleManager.bypassShapeshiftRatelimits.Enabled, "Bypass ratelimits for Shapeshift RPC");

			ModuleManager.antiCrash.Enabled = Controls.Toggle(ModuleManager.antiCrash.Enabled, "Protect against ReportDeadBody lobby crash exploit");

			// Overloads
			GUILayout.Space(5);
			GUILayout.Label("Anti Overload:");
			ModuleManager.antiOverload.Enabled = Controls.Toggle(ModuleManager.antiOverload.Enabled, "Enabled");
			ModuleManager.antiOverload.BlockLargeGameMessages = Controls.Toggle(ModuleManager.antiOverload.BlockLargeGameMessages, "Block large game messages");
			ModuleManager.antiOverload.BlockInvalidGameDataMessages = Controls.Toggle(ModuleManager.antiOverload.BlockInvalidGameDataMessages, "Block invalid game data message types");
			ModuleManager.antiOverload.HardenedPacketUIntDeserializer = Controls.Toggle(ModuleManager.antiOverload.HardenedPacketUIntDeserializer, "Use hardened packed int deserializer");
			ModuleManager.antiOverload.BlockVotingCompleteOverload = Controls.Toggle(ModuleManager.antiOverload.BlockVotingCompleteOverload, "Protect against VotingComplete overloads");

			// Kick Exploits
			GUILayout.Space(5);
			GUILayout.Label("Anti Kick:");
			ModuleManager.antiKick.Enabled = Controls.Toggle(ModuleManager.antiKick.Enabled, "Enabled");
			ModuleManager.antiKick.BlockVotekicks = Controls.Toggle(ModuleManager.antiKick.BlockVotekicks, "Protect against votekicks as host");
			ModuleManager.antiKick.BlockHostOnlyRpcExploit = Controls.Toggle(ModuleManager.antiKick.BlockHostOnlyRpcExploit, "Protect against host-only RPC kick exploit");
		}
	}
}