using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace BaneMenu.modules.visuals
{
	/// <summary>
	/// SickoMenu RevealRoles - faithful:
	/// - PlayerControl.FixedUpdate nameText
	/// - MeetingHud.Update NameText (role line + color)
	/// - ChatBubble.SetName (role color + size 50% role prefix in-game)
	/// On disable: restore plain names (Sicko stops writing; we reset so text does not stick).
	/// </summary>
	internal class RevealRoles : Module
	{
		public RevealRoles() : base("RevealRoles") { }

		static RevealRoles Instance => ModuleManager.revealRoles;

		// ---- Player world names (Sicko PlayerControl FixedUpdate) ----
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
		static class PcFixedUpdate
		{
			static void Postfix(PlayerControl __instance)
			{
				if (Instance == null || !Instance.Enabled) return;
				if (__instance == null) return;
				ApplyWorld(__instance);
			}
		}

		// ---- Meeting (Sicko MeetingHud_Update) ----
		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
		static class MeetingUpdate
		{
			static void Postfix(MeetingHud __instance)
			{
				if (Instance == null || !Instance.Enabled) return;
				if (__instance == null || __instance.playerStates == null) return;
				foreach (PlayerVoteArea area in __instance.playerStates)
				{
					if (area == null) continue;
					try
					{
						var data = GameData.Instance != null
							? GameData.Instance.GetPlayerById(area.PlayerId) : null;
						if (data == null) continue;
						ApplyMeeting(area, data);
					}
					catch { }
				}
			}
		}

		// ---- Chat (Sicko ChatBubble_SetName) ----
		// Exact Sicko flow: Prefix mutates playerName + color, then original SetName runs.
		// No ChatController patches (wrong signatures caused Harmony fatal).
		[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
		static class ChatBubbleSetName
		{
			static void Prefix(ref string playerName, ref Color color)
			{
				if (Instance == null || !Instance.Enabled) return;
				if (!InGameOrLobby()) return;
				if (playerName == null) return;

				try
				{
					if (GameData.Instance == null || GameData.Instance.AllPlayers == null) return;

					foreach (NetworkedPlayerInfo data in GameData.Instance.AllPlayers)
					{
						if (data == null) continue;
						// Sicko: playerName == NetworkedPlayerInfo_get_PlayerName (exact)
						if (data.PlayerName != playerName) continue;

						RoleTypes role = GetRole(data);
						// Sicko: color = RevealRoles ? GetRoleColor : (impostor teammate red / white)
						color = GetRoleColor(role);

						// Sicko: only in-game prefix role at 50% size
						if (IsInGame())
						{
							string roleName = FormatRoleName(role);
							playerName = $"<size=50%>{roleName}</size> {playerName}";
						}
						break;
					}
				}
				catch { }
			}
		}

		static bool InGameOrLobby()
		{
			try
			{
				if (AmongUsClient.Instance == null) return false;
				var st = AmongUsClient.Instance.GameState;
				return st == InnerNetClient.GameStates.Joined
					|| st == InnerNetClient.GameStates.Started;
			}
			catch { return false; }
		}

		static bool IsLobby()
		{
			try
			{
				return AmongUsClient.Instance != null
					&& AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined
					&& ShipStatus.Instance == null;
			}
			catch { return false; }
		}

		static bool IsInGame()
		{
			try
			{
				return AmongUsClient.Instance != null
					&& AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started;
			}
			catch { return false; }
		}

		static RoleTypes GetRole(NetworkedPlayerInfo data)
		{
			try { if (data.Role != null) return data.Role.Role; } catch { }
			try { return data.RoleType; } catch { }
			return RoleTypes.Crewmate;
		}

		static string FormatRoleName(RoleTypes role)
		{
			string n = role.ToString();
			return n == "SpiritGuide" ? "Influencer" : n;
		}

		static Color GetRoleColor(RoleTypes role)
		{
			try { if (RoleManager.IsImpostorRole(role)) return Palette.ImpostorRed; } catch { }
			switch (role)
			{
				case RoleTypes.Engineer: return new Color(0.55f, 0.45f, 0.15f, 1f);
				case RoleTypes.Scientist: return new Color(0.2f, 0.7f, 0.9f, 1f);
				case RoleTypes.GuardianAngel: return new Color(0.7f, 0.9f, 1f, 1f);
				case RoleTypes.Shapeshifter: return new Color(0.9f, 0.4f, 0.1f, 1f);
				case RoleTypes.Phantom: return new Color(0.6f, 0.3f, 0.7f, 1f);
				case RoleTypes.Tracker: return new Color(0.4f, 0.8f, 0.4f, 1f);
				case RoleTypes.Noisemaker: return new Color(0.9f, 0.5f, 0.7f, 1f);
				case RoleTypes.Detective: return new Color(0.5f, 0.6f, 0.9f, 1f);
				default: return Color.white;
			}
		}

		static string Hex(Color c) => ColorUtility.ToHtmlStringRGBA(c);

		static void ApplyWorld(PlayerControl pc)
		{
			if (!InGameOrLobby()) return;
			if (pc == null || pc.Data == null || pc.Data.Disconnected) return;
			try
			{
				if (pc.cosmetics == null || pc.cosmetics.nameText == null) return;
				try { if (!pc.Visible) return; } catch { }

				string playerName = pc.Data.PlayerName ?? "";
				RoleTypes roleType = GetRole(pc.Data);
				string roleName = FormatRoleName(roleType);
				string hex = Hex(GetRoleColor(roleType));

				if (IsLobby())
				{
					// Sicko lobby: colored name only
					playerName = $"<#{hex}>{playerName}</color>";
				}
				else
				{
					// Sicko in-game meeting-style world name
					int total = 0, done = 0;
					try
					{
						if (pc.Data.Tasks != null)
						{
							total = pc.Data.Tasks.Count;
							for (int i = 0; i < pc.Data.Tasks.Count; i++)
								if (pc.Data.Tasks[i] != null && pc.Data.Tasks[i].Complete) done++;
						}
					}
					catch { }

					bool impostor = false;
					try { impostor = RoleManager.IsImpostorRole(roleType); } catch { }

					if (total == 0 || (impostor && done == 0))
						playerName = $"<#{hex}><size=1.4>{roleName}\n</size>{playerName}\n<size=1.4><#00000000>0</color></color>";
					else
						playerName = $"<#{hex}><size=1.4>{roleName} ({done}/{total})\n</size>{playerName}\n<size=1.4><#00000000>0</color></color>";
				}

				if (ModuleManager.modDetection != null && ModuleManager.modDetection.IsBaneUser(pc))
					playerName = ModDetection.TagRich + "\n" + playerName;

				var tmp = pc.cosmetics.nameText;
				tmp.richText = true;
				tmp.text = playerName;
			}
			catch { }
		}

		static void ApplyMeeting(PlayerVoteArea area, NetworkedPlayerInfo data)
		{
			// Sicko MeetingHud_Update RevealRoles block
			if (area.NameText == null) return;
			try
			{
				string playerName = data.PlayerName ?? "";
				RoleTypes roleType = GetRole(data);
				string roleName = FormatRoleName(roleType);
				Color roleColor = GetRoleColor(roleType);
				string hex = Hex(roleColor);

				int total = 0, done = 0;
				try
				{
					if (data.Tasks != null)
					{
						total = data.Tasks.Count;
						for (int i = 0; i < data.Tasks.Count; i++)
							if (data.Tasks[i] != null && data.Tasks[i].Complete) done++;
					}
				}
				catch { }

				bool disconnected = false;
				try { disconnected = data.Disconnected; } catch { }

				if (disconnected)
					playerName = $"<size=1.2>{roleName} (D/C)\n</size>{playerName}\n<size=1.2><#00000000>0";
				else if (total == 0 || (RoleManager.IsImpostorRole(roleType) && done == 0))
					playerName = $"<size=1.2>{roleName}\n</size>{playerName}\n<size=1.2><#00000000>0";
				else
					playerName = $"<size=1.2>{roleName} ({done}/{total})\n</size>{playerName}\n<size=1.2><#00000000>0";

				playerName = $"<#{hex}>{playerName}</color>";

				if (ModuleManager.modDetection != null && ModuleManager.modDetection.IsBaneUser(data.PlayerId))
					playerName = ModDetection.TagRich + "\n" + playerName;

				area.NameText.richText = true;
				area.NameText.text = playerName;
				try { area.NameText.color = Color.white; } catch { }
			}
			catch { }
		}

		/// <summary>Restore plain names when Reveal Roles is turned off.</summary>
		static void ResetAllNames()
		{
			try
			{
				if (PlayerControl.AllPlayerControls != null)
				{
					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (pc == null || pc.Data == null) continue;
						if (pc.cosmetics == null || pc.cosmetics.nameText == null) continue;
						try
						{
							pc.cosmetics.nameText.richText = true;
							pc.cosmetics.nameText.text = pc.Data.PlayerName ?? "";
							// Restore default name color (white); impostor red reapplied by game if needed
							pc.cosmetics.nameText.color = Color.white;
						}
						catch { }
					}
				}
			}
			catch { }

			try
			{
				if (MeetingHud.Instance != null && MeetingHud.Instance.playerStates != null)
				{
					foreach (PlayerVoteArea area in MeetingHud.Instance.playerStates)
					{
						if (area == null || area.NameText == null) continue;
						var data = GameData.Instance != null
							? GameData.Instance.GetPlayerById(area.PlayerId) : null;
						if (data == null) continue;
						area.NameText.richText = true;
						area.NameText.text = data.PlayerName ?? "";
						area.NameText.color = Color.white;
					}
				}
			}
			catch { }
		}

		protected override void OnDisable()
		{
			ResetAllNames();
		}
	}
}
