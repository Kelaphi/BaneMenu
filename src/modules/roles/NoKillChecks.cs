using HarmonyLib;
using BaneMenu.network;
using Hazel;
using UnityEngine;

namespace BaneMenu.modules.roles
{
	internal class NoKillChecks : Module
	{
		public NoKillChecks() : base("NoKillChecks") { }

		private static NoKillChecks Instance
		{
			get { return ModuleManager.noKillChecks; }
		}

		public bool KillOtherImpostors { get; set; } = false;
		public bool KillAsPhantom { get; set; } = false;
		public bool NoKillCooldown { get; set; } = false;
		public bool KillGhosts { get; set; } = false;

		/// <summary>
		/// Allow targeting players who are in a vent. On kill as non-host, force Exit via
		/// VentilationSystem UpdateSystem then murder (host can murder in-vent directly).
		/// </summary>
		public bool KillPlayersInVents { get; set; } = false;

		private static ushort ventSeq = 50;

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetKillTimer))]
		class KillTimer
		{
			static void Prefix(PlayerControl __instance, ref float time)
			{
				if (!Instance.Enabled || !Instance.NoKillCooldown || __instance != PlayerControl.LocalPlayer) return;
				time = 0.0f;
			}
		}

		[HarmonyPatch(typeof(ImpostorRole), nameof(ImpostorRole.IsValidTarget))]
		class NoImpKillChecks
		{
			static bool Prefix(NetworkedPlayerInfo target, ref bool __result)
			{
				if (!Instance.Enabled) return true;
				__result = IsValidTarget(target);
				return false;
			}
		}

		[HarmonyPatch(typeof(PhantomRole), nameof(PhantomRole.IsValidTarget))]
		class NoPhantomKillChecks
		{
			static bool Prefix(PhantomRole __instance, NetworkedPlayerInfo target, ref bool __result)
			{
				if (!Instance.Enabled) return true;
				__result = IsValidTarget(target) && (!__instance.isInvisible || Instance.KillAsPhantom);
				return false;
			}
		}

		private static bool IsValidTarget(NetworkedPlayerInfo target)
		{
			if (target == null || target == PlayerControl.LocalPlayer.Data || target.Disconnected)
				return false;

			if (target.IsDead && !Instance.KillGhosts)
				return false;

			if (RoleManager.IsImpostorRole(target.RoleType) && !Instance.KillOtherImpostors)
				return false;

			// Vanilla blocks kills on players in vents unless we opt in
			PlayerControl pc = target.Object;
			if (pc != null && pc.inVent && !Instance.KillPlayersInVents)
				return false;

			return true;
		}

		/// <summary>
		/// Force a player out of a vent via VentilationSystem.Exit UpdateSystem (works without host
		/// the same way other vent AC updates do), then the kill can proceed.
		/// </summary>
		public static void ForceExitVent(PlayerControl player)
		{
			if (player == null || ShipStatus.Instance == null) return;

			byte ventId = 0;
			try
			{
				// Best-effort: closest vent to their body / current position
				if (ShipStatus.Instance.AllVents != null && ShipStatus.Instance.AllVents.Count > 0)
				{
					Vent closest = null;
					float best = float.MaxValue;
					Vector2 pos = player.GetTruePosition();
					foreach (Vent v in ShipStatus.Instance.AllVents)
					{
						if (v == null) continue;
						float d = Vector2.Distance(pos, v.transform.position);
						if (d < best)
						{
							best = d;
							closest = v;
						}
					}
					if (closest != null)
						ventId = (byte)closest.Id;
				}
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"ForceExitVent vent id resolve: {e.Message}");
			}

			try
			{
				BatchedMessage batch = new BatchedMessage(player.OwnerId);

				MessageWriter writer = MessageWriter.Get(SendOption.None);
				writer.Write(++ventSeq);
				writer.Write((byte)VentilationSystem.Operation.Exit);
				writer.Write(ventId);

				// Send as the venting player’s system update when possible
				batch.QueueUpdateSystem(player, SystemTypes.Ventilation, writer);
				writer.Recycle();
				batch.FinishBatch();

				Bane.Log.LogInfo($"ForceExitVent: Exit op for {player.Data?.PlayerName} ventId={ventId}");
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning($"ForceExitVent batch failed: {e.Message}");
			}
		}

		// CheckMurder / host path blocks in-vent kills; pull them out then murder
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))]
		class KillBypass
		{
			static bool Prefix(PlayerControl __instance, PlayerControl target)
			{
				if (!Instance.Enabled) return true;

				bool targetInVent = target != null && target.inVent;

				if (Instance.KillPlayersInVents && targetInVent && __instance == PlayerControl.LocalPlayer)
				{
					// Host (or no AC): MurderPlayer works while target is still in a vent
					if (Utilities.IsHostEffective() || !Utilities.IsAnticheatPresent())
					{
						__instance.RpcMurderPlayer(target, true);
						return false;
					}

					// Non-host: force Exit via UpdateSystem, then murder
					ForceExitVent(target);
					try
					{
						__instance.RpcMurderPlayer(target, true);
					}
					catch
					{
						return true;
					}
					return false;
				}

				if (!Utilities.IsHostEffective() && Utilities.IsAnticheatPresent()) return true;

				__instance.RpcMurderPlayer(target, true);
				return false;
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckVanish))]
		class VanishBypass
		{
			static bool Prefix(PlayerControl __instance)
			{
				if (!Instance.Enabled || !Instance.KillAsPhantom) return true;
				BatchedMessage batch = new BatchedMessage();
				batch.QueueVanish(__instance);
				batch.FinishBatch();
				return false;
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckAppear))]
		class AppearBypass
		{
			static bool Prefix(PlayerControl __instance, bool shouldAnimate)
			{
				if (!Instance.Enabled || !Instance.KillAsPhantom) return true;
				BatchedMessage batch = new BatchedMessage();
				batch.QueueAppear(__instance, shouldAnimate);
				batch.FinishBatch();
				return false;
			}
		}
	}
}
