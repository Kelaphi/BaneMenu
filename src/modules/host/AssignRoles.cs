using AmongUs.GameOptions;
using HarmonyLib;
using System.Collections.Generic;

namespace BaneMenu.modules.host
{
	internal class AssignRoles : Module
	{
		public AssignRoles() : base("AssignRoles")
		{
			/* default off until config */ base.Enabled = false;
		}

		private static AssignRoles Instance => ModuleManager.assignRoles;

		/// <summary>Legacy field kept for config compatibility.</summary>
		public RoleTypes AssignedRole { get; set; } = RoleTypes.Viper;

		/// <summary>Per-player forced roles (PlayerId → role). Runtime only - not in config.</summary>
		public readonly Dictionary<byte, RoleTypes> PlayerAssignments = new Dictionary<byte, RoleTypes>();

		public readonly HashSet<byte> AssignedPlayers = new HashSet<byte>();

		public void SetPlayerRole(byte playerId, RoleTypes role, bool enabled)
		{
			if (!enabled)
			{
				AssignedPlayers.Remove(playerId);
				PlayerAssignments.Remove(playerId);
				return;
			}
			AssignedPlayers.Add(playerId);
			PlayerAssignments[playerId] = role;
		}

		public bool TryGetAssignment(byte playerId, out RoleTypes role)
		{
			if (AssignedPlayers.Contains(playerId) && PlayerAssignments.TryGetValue(playerId, out role))
				return true;
			role = default;
			return false;
		}

		[HarmonyPatch(typeof(LogicRoleSelectionNormal), nameof(LogicRoleSelectionNormal.AssignRolesFromList))]
		class AssignRolesPatch
		{
			static void Prefix(
				ref Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo> players,
				ref Il2CppSystem.Collections.Generic.List<RoleTypes> roleList,
				ref int rolesAssigned)
			{
				if (!Instance.Enabled || !Utilities.IsHostEffective()) return;
				if (Instance.AssignedPlayers.Count == 0) return;

				var toAssign = new List<(NetworkedPlayerInfo info, RoleTypes role, int index)>();
				for (int i = 0; i < players.Count; i++)
				{
					NetworkedPlayerInfo info = players[i];
					if (info == null || info.Object == null) continue;
					if (!Instance.TryGetAssignment(info.PlayerId, out RoleTypes role)) continue;
					toAssign.Add((info, role, i));
				}

				toAssign.Sort((a, b) => b.index.CompareTo(a.index));
				foreach (var (info, role, index) in toAssign)
				{
					Bane.Log.LogInfo($"AssignRoles: forcing {info.PlayerName} ({info.PlayerId}) → {role}");
					players.RemoveAt(index);

					Il2CppSystem.Predicate<RoleTypes> pred = (Il2CppSystem.Predicate<RoleTypes>)(r => r == role);
					int roleIndex = roleList.FindIndex(pred);
					if (roleIndex != -1)
						roleList.RemoveAt(roleIndex);

					PlayerControl pc = info.Object;
					if (pc == null) continue;

					if (RoleManager.IsGhostRole(role) && players.Count == 0)
						pc.RpcSetRole(RoleManager.IsImpostorRole(role) ? RoleTypes.Impostor : RoleTypes.Crewmate);

					pc.RpcSetRole(role);
					rolesAssigned++;
				}
			}
		}

		private void OnGameStart()
		{
			if (Utilities.IsHostEffective() || Utilities.IsAnticheatPresent()) return;
			if (AssignedPlayers.Count == 0) return;

			Bane.Log.LogMessage("Host-authoritative lobby: applying forced roles via RpcSetRole");
			foreach (PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if (player == null || player.Data == null) continue;
				if (!TryGetAssignment(player.PlayerId, out RoleTypes role)) continue;
				player.RpcSetRole(role, false);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnGameStart += OnGameStart;
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnGameStart -= OnGameStart;
		}
	}
}
