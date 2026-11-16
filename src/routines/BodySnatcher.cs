using System.Collections.Generic;
using BaneMenu.modules;
using UnityEngine;

namespace BaneMenu.routines
{
	/// <summary>
	/// Maximum vent harassment: kick on enter, exit, and anyone the vent system still thinks is inside.
	/// </summary>
	public class BodySnatcherRoutine : Routine
	{
		public BodySnatcherRoutine() : base("VentExile") { }

		private float lastKickTime = 0f;
		private float lastSweepTime = 0f;
		private const float KICK_COOLDOWN = 0.25f;
		private const float SWEEP_INTERVAL = 0.5f;

		private void TryKick(PlayerControl player, string reason)
		{
			if (player == null || player.Data == null) return;
			if (player == PlayerControl.LocalPlayer) return;
			if (player.Data.IsDead || player.Data.Disconnected) return;
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
			if (PlayerControl.LocalPlayer.Data.IsDead) return;
			if (player.OwnerId == AmongUsClient.Instance.HostId) return;
			if (Whitelist.ShouldSkip(player)) return;
			if (Time.time - lastKickTime < KICK_COOLDOWN) return;

			lastKickTime = Time.time;
			Bane.notifications.Send("Vent Exile", $"{player.Data.PlayerName}: {reason}", 2);
			Utilities.KickPlayer(player);
		}

		private void OnPlayerEnterVent(PlayerControl player, byte ventId)
		{
			if (!Enabled) return;
			TryKick(player, "entered a vent");
		}

		private void OnPlayerExitVent(PlayerControl player, byte ventId)
		{
			if (!Enabled) return;
			TryKick(player, "left a vent");
		}

		public override void Run()
		{
			if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
			if (PlayerControl.LocalPlayer.Data.IsDead) return;
			if (ShipStatus.Instance == null) return;
			if (Time.time - lastSweepTime < SWEEP_INTERVAL) return;
			lastSweepTime = Time.time;

			// Sweep: anyone the game still marks as in-vent
			VentilationSystem ventSys = null;
			if (ShipStatus.Instance.Systems != null &&
			    ShipStatus.Instance.Systems.ContainsKey(SystemTypes.Ventilation))
			{
				ventSys = ShipStatus.Instance.Systems[SystemTypes.Ventilation].Cast<VentilationSystem>();
			}
			if (ventSys == null) return;

			// PlayersInsideVents is the authoritative "who is in a vent" map on many builds
			try
			{
				var inside = ventSys.PlayersInsideVents;
				if (inside == null) return;

				foreach (var kvp in inside)
				{
					byte playerId = kvp.Key;
					NetworkedPlayerInfo info = GameData.Instance.GetPlayerById(playerId);
					if (info == null || info.Object == null) continue;
					TryKick(info.Object, "still in a vent");
				}
			}
			catch
			{
				// Field name may differ across versions - enter/exit hooks still work
			}
		}

		private void OnDisconnect()
		{
			Enabled = false;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			lastKickTime = 0f;
			lastSweepTime = 0f;
			EventCoordinator.OnPlayerEnterVent += OnPlayerEnterVent;
			EventCoordinator.OnPlayerExitVent += OnPlayerExitVent;
			EventCoordinator.OnDisconnect += OnDisconnect;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnPlayerEnterVent -= OnPlayerEnterVent;
			EventCoordinator.OnPlayerExitVent -= OnPlayerExitVent;
			EventCoordinator.OnDisconnect -= OnDisconnect;
		}
	}
}
