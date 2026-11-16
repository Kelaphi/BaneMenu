using AmongUs.GameOptions;
using BaneMenu.modules;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class RolesSection : Section
	{
		public RolesSection() : base("Roles") { }

		static string[] _roleNames;
		static RoleTypes[] _roleValues;

		static void EnsureRoleOptions()
		{
			if (_roleNames != null) return;
			var list = Controls.RolesList;
			// Index 0 = no forced assignment
			_roleValues = new RoleTypes[list.Count];
			_roleNames = new string[list.Count + 1];
			_roleNames[0] = "Default (no force)";
			for (int i = 0; i < list.Count; i++)
			{
				_roleValues[i] = list[i];
				_roleNames[i + 1] = list[i].ToString() == "SpiritGuide" ? "Influencer" : list[i].ToString();
			}
		}

		static int IndexForPlayer(byte pid, BaneMenu.modules.host.AssignRoles assign)
		{
			EnsureRoleOptions();
			if (!assign.TryGetAssignment(pid, out RoleTypes role))
				return 0;
			for (int i = 0; i < _roleValues.Length; i++)
				if (_roleValues[i] == role) return i + 1;
			return 0;
		}

		public override void Render()
		{
			EnsureRoleOptions();
			var assign = ModuleManager.assignRoles;

			assign.Enabled = Controls.Toggle(assign.Enabled, "Assign roles for next round");
			GUILayout.Label("Choose a role per player. Default = normal assignment.");
			GUILayout.Label("Not saved to config.");
			GUILayout.Space(4);

			if (PlayerControl.AllPlayerControls == null || PlayerControl.AllPlayerControls.Count == 0)
			{
				GUILayout.Label("No players in lobby.");
				return;
			}

			foreach (PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if (player == null || player.Data == null) continue;

				byte pid = player.PlayerId;
				string name = player.Data.PlayerName ?? $"Player {pid}";
				if (player.AmOwner) name += " (You)";

				int idx = IndexForPlayer(pid, assign);
				int newIdx = Controls.Dropdown(name, _roleNames, idx, "role_dd_" + pid);
				if (newIdx != idx)
				{
					if (newIdx <= 0)
					{
						assign.SetPlayerRole(pid, default, false);
					}
					else
					{
						RoleTypes chosen = _roleValues[newIdx - 1];
						assign.SetPlayerRole(pid, chosen, true);
						if (!assign.Enabled)
							assign.Enabled = true;
					}
				}
			}

		}

		public static void UpdateRole(RoleTypes role)
		{
			Bane.Log.LogInfo($"Updating role to {role}");

			bool isGhost = RoleManager.IsGhostRole(role);
			if (HudManager.Instance != null && HudManager.Instance.ReportButton != null)
				HudManager.Instance.ReportButton.gameObject.SetActive(!isGhost);

			if (RoleManager.Instance != null && PlayerControl.LocalPlayer != null)
				RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, role);

			if (Utilities.IsHostEffective() && PlayerControl.LocalPlayer != null)
			{
				Bane.Log.LogInfo("Since we are host, we can send the SetRole RPC to sync the new role to the server");
				PlayerControl.LocalPlayer.RpcSetRole(role, true);
			}

			Bane.notifications.Send("Update Role", $"Your role has been updated to {role}.");
		}
	}
}
