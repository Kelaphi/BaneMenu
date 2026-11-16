using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace BaneMenu.modules.host
{
	/// <summary>
	/// Host-only: set KillCooldown to NaN for targets so their kill button disappears.
	/// </summary>
	internal class DisableKillButton : Module
	{
		public DisableKillButton() : base("Disable Kill Button") { }

		/// <summary>All players (except host optional - still applies to everyone marked).</summary>
		public bool Everyone;

		public readonly HashSet<byte> Targets = new HashSet<byte>();

		float _reapply;

		public bool IsDisabled(byte playerId) => Everyone || Targets.Contains(playerId);

		public void SetPlayer(PlayerControl pc, bool on)
		{
			if (pc == null || pc.Data == null) return;
			if (on) Targets.Add(pc.PlayerId);
			else Targets.Remove(pc.PlayerId);
			if (on) ApplyTo(pc);
			else RestoreDefault(pc);
		}

		public static void ApplyTo(PlayerControl target)
		{
			if (target == null || AmongUsClient.Instance == null) return;
			if (!Utilities.IsHostEffective()) return;
			try
			{
				if (GameManager.Instance == null || GameManager.Instance.LogicOptions == null) return;
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(
					GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetFloat(FloatOptionNames.KillCooldown, float.NaN);
				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("DisableKillButton Apply: " + e.Message);
			}
		}

		public static void RestoreDefault(PlayerControl target)
		{
			if (target == null || AmongUsClient.Instance == null) return;
			if (!Utilities.IsHostEffective()) return;
			try
			{
				if (GameManager.Instance == null || GameManager.Instance.LogicOptions == null) return;
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(
					GameManager.Instance.LogicOptions.currentGameOptions);
				// Restore room kill cooldown (or 30s fallback)
				float cd = 30f;
				try
				{
					cd = GameManager.Instance.LogicOptions.currentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
					if (float.IsNaN(cd) || cd <= 0f) cd = 30f;
				}
				catch { }
				// If room is already NaN, force a sane value
				if (float.IsNaN(cd)) cd = 30f;
				gameOptions.SetFloat(FloatOptionNames.KillCooldown, cd);
				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("DisableKillButton Restore: " + e.Message);
			}
		}

		public void ApplyAllMarked()
		{
			if (!Utilities.IsHostEffective()) return;
			if (PlayerControl.AllPlayerControls == null) return;
			foreach (var p in PlayerControl.AllPlayerControls)
			{
				if (p == null || p.Data == null || p.Data.Disconnected) continue;
				if (IsDisabled(p.PlayerId))
					ApplyTo(p);
			}
		}

		public void Tick()
		{
			if (!Everyone && Targets.Count == 0) return;
			if (!Utilities.IsHostEffective()) return;
			_reapply -= Time.deltaTime;
			if (_reapply > 0f) return;
			_reapply = 2f; // re-assert NaN periodically (meetings / role changes reset UI)
			ApplyAllMarked();
		}
	}
}
