using AmongUs.GameOptions;
using Hazel;
using BaneMenu.network;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BaneMenu
{
	internal class Utilities
	{
		// Lazy - never touch HatManager in a static field initializer (causes GC "unknown thread" crashes on boot)
		private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<SkinData> allSkins => HatManager.Instance?.allSkins;
		private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<HatData> allHats => HatManager.Instance?.allHats;
		private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<VisorData> allVisors => HatManager.Instance?.allVisors;
		private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PetData> allPets => HatManager.Instance?.allPets;
		private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<NamePlateData> allNameplates => HatManager.Instance?.allNamePlates;

		private static readonly System.Random rnd = new System.Random();

		public static int GetRandomUnusedColor()
		{
			List<int> colors = Enumerable.Range(0, 18).ToList();

			foreach(PlayerControl player in PlayerControl.AllPlayerControls)
			{
				colors.Remove(player.Data.DefaultOutfit.ColorId);
			}

			// Some modded lobbies may have more than 18 players, which means there will not be enough unique colors for everyone
			// so we should take that edge case into account
			return colors.Count != 0 ? colors[rnd.Next(0, colors.Count)] : rnd.Next(0, 18);
		}

		public static void RandomizePlayer(bool inGame = false)
		{
			var hats = allHats;
			var visors = allVisors;
			var skins = allSkins;
			var pets = allPets;
			var plates = allNameplates;

			if (hats == null || visors == null || skins == null || pets == null || plates == null)
			{
				Bane.Log.LogWarning("RandomizePlayer: HatManager not ready");
				return;
			}
			if (hats.Length == 0 || visors.Length == 0 || skins.Length == 0 || pets.Length == 0 || plates.Length == 0)
				return;

			if(inGame)
			{
				if (PlayerControl.LocalPlayer == null) return;
				PlayerControl.LocalPlayer.CmdCheckColor((byte)GetRandomUnusedColor());

				PlayerControl.LocalPlayer.RpcSetHat(hats[rnd.Next(0, hats.Length)].ProductId);
				PlayerControl.LocalPlayer.RpcSetVisor(visors[rnd.Next(0, visors.Length)].ProductId);
				PlayerControl.LocalPlayer.RpcSetSkin(skins[rnd.Next(0, skins.Length)].ProductId);
				PlayerControl.LocalPlayer.RpcSetPet(pets[rnd.Next(0, pets.Length)].ProductId);
			}
			else
			{
				if (AccountManager.Instance != null)
					AccountManager.Instance.RandomizeName();

				PlayerCustomization.EquipHat(hats[rnd.Next(0, hats.Length)]);
				PlayerCustomization.EquipVisor(visors[rnd.Next(0, visors.Length)]);
				PlayerCustomization.EquipSkin(skins[rnd.Next(0, skins.Length)]);
				PlayerCustomization.EquipPet(pets[rnd.Next(0, pets.Length)]);
				PlayerCustomization.EquipNameplate(plates[rnd.Next(0, plates.Length)]);
			}
		}

		public static PlayerControl GetRandomPlayer(bool excludeHost = false, bool excludeDead = false, bool excludeImposters = false, bool excludeSelf = true)
		{
			List<PlayerControl> validPlayers = new List<PlayerControl>();

			foreach(PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if(
					(excludeSelf && AmongUsClient.Instance.ClientId == player.OwnerId) ||
					(excludeHost && AmongUsClient.Instance.HostId == player.OwnerId) ||
					(excludeDead && player.Data.IsDead) ||
					(excludeImposters && RoleManager.IsImpostorRole(player.Data.RoleType))
				) continue;

				validPlayers.Add(player);
			}

			if(validPlayers.Count == 0) return null;

			return validPlayers[rnd.Next(validPlayers.Count)];
		}

		public static void CopyPlayer(PlayerControl player)
		{
			NetworkedPlayerInfo.PlayerOutfit outfit = player.CurrentOutfit;

			bool hasAnticheat = IsAnticheatPresent();

			BatchedMessage batch = new BatchedMessage();

			// We cannot change the name of our player in server-authoritative lobbies, even as the host
			if(!hasAnticheat)
			{
				batch.QueueSetName(PlayerControl.LocalPlayer, outfit.PlayerName);
			}

			if(!hasAnticheat || Utilities.IsHostEffective())
			{
				batch.QueueSetColor(PlayerControl.LocalPlayer, (byte)outfit.ColorId);
			}

			batch.QueueSetNameplateStr(PlayerControl.LocalPlayer, outfit.NamePlateId);
			batch.QueueSetHatStr(PlayerControl.LocalPlayer, outfit.HatId);
			batch.QueueSetVisorStr(PlayerControl.LocalPlayer, outfit.VisorId);
			batch.QueueSetSkinStr(PlayerControl.LocalPlayer, outfit.SkinId);
			batch.QueueSetPetStr(PlayerControl.LocalPlayer, outfit.PetId);

			batch.FinishBatch();
		}

		public static void AttemptStartMeeting(PlayerControl reporter, NetworkedPlayerInfo target)
		{
			Bane.Log.LogInfo($"Attempting to start a meeting for {reporter.Data.PlayerName}");

			bool hasAnticheat = IsAnticheatPresent();

			if(hasAnticheat && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
			{
				Bane.notifications.Send("Start Meeting", "The game must have started in order for this feature to work.");
				return;
			}

			if(Utilities.IsHostEffective())
			{
				Bane.Log.LogInfo($"We are the host so we can directly use the StartMeeting RPC");

				if(ShipStatus.Instance == null)
				{
					Bane.notifications.Send("Start Meeting", "There must be a valid instance of ShipStatus for this feature to work.");
				}
				else
				{
					OpenMeeting(reporter, target);
				}

				return;
			}

			Bane.Log.LogInfo("We are not the host so we have to use the ReportDeadBody RPC");

			if(hasAnticheat && reporter != PlayerControl.LocalPlayer)
			{
				Bane.notifications.Send("Start Meeting", "You must be the host of the lobby to make another player start a meeting.");
				return;
			}

			if(reporter.Data.IsDead)
			{
				Bane.notifications.Send("Start Meeting", "You can only call meetings or report bodies if you are alive.");
				return;
			}

			if(hasAnticheat && target != null)
			{
				if(!target.IsDead)
				{
					Bane.notifications.Send("Start Meeting", "You can only report bodies of players who have died in this round.");
					return;
				}

				if(!DoesDeadBodyExist(target.PlayerId))
				{
					Bane.notifications.Send("Start Meeting", "Unable to find a dead body for this player, you can only report a player's body if they have died this round and their body has not dissolved.");
					return;
				}
			}

			reporter.CmdReportDeadBody(target);
		}

		public static void OpenMeeting(PlayerControl reporter, NetworkedPlayerInfo target)
		{
			MeetingRoomManager.Instance.AssignSelf(reporter, target);
			reporter.RpcStartMeeting(target);
			HudManager.Instance.OpenMeetingRoom(reporter);
		}

		public static bool DoesDeadBodyExist(byte playerId)
		{
			foreach(Collider2D collider in Physics2D.OverlapCircleAll(new Vector2(0, 0), 99999f, Constants.PlayersOnlyMask))
			{
				if(collider.tag != "DeadBody") continue;

				DeadBody bodyComponent = collider.GetComponent<DeadBody>();
				if(bodyComponent && bodyComponent.ParentId == playerId)
				{
					return true;
				}
			}

			return false;
		}

		public static void ShapeshiftPlayer(PlayerControl victim, PlayerControl target, bool shouldAnimate = true)
		{
			bool hasAnticheat = IsAnticheatPresent();

			if(hasAnticheat && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Shapeshift Player", "You must be the host of the lobby in order to use this feature.");
				return;
			}

			if(hasAnticheat && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
			{
				Bane.notifications.Send("Shapeshift Player", "The game must have started for this option to work.");
				return;
			}

			BatchedMessage batch = new BatchedMessage();

			// The vanilla anticheat will ban the host if they attempt to send the Shapeshift RPC for a player whose role is not Shapeshifter
			// To get around this, we temporarily change the player's role to Shapeshifter, make them shapeshift, and revert them back to their previous role
			if(hasAnticheat && victim.Data.RoleType != RoleTypes.Shapeshifter)
			{
				RoleTypes currentRole = victim.Data.RoleType;

				// The client that we're attempting to frame shouldn't notice anything as during role selection the SetRole RPC is sent with the canOverrideRole option set to false
				// meaning any future SetRole RPCs will be ignored unless the new role is a ghost role
				// Just in case this ever gets changed in the future, we could broadcast the SetRole RPC to a junk client ID instead of everyone to avoid the client knowing they became a Shapeshifter
				batch.QueueSetRole(victim, RoleTypes.Shapeshifter, true);
				batch.QueueShapeshift(victim, target, shouldAnimate);
				batch.QueueSetRole(victim, currentRole, true);
			}
			else
			{
				batch.QueueShapeshift(victim, target, shouldAnimate);
			}

			batch.FinishBatch();
		}

		public static MapNames GetCurrentMap()
		{
			// If Among Us has not fully loaded in yet then default to The Skeld
			if(AmongUsClient.Instance == null) return MapNames.Skeld;

			// Fall back to current map according to game options if ShipStatus does not exist
			if(ShipStatus.Instance == null)
			{
				if(AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
				{
					return (MapNames)AmongUsClient.Instance.TutorialMapId;
				}
				else
				{
					return (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
				}
			}

			return (SpawnType)ShipStatus.Instance.SpawnId switch
			{
				SpawnType.SkeldShipStatus => MapNames.Skeld,
				SpawnType.DleksShipStatus => MapNames.Dleks,
				SpawnType.MiraShipStatus => MapNames.MiraHQ,
				SpawnType.PolusShipStatus => MapNames.Polus,
				SpawnType.AirshipShipStatus => MapNames.Airship,
				SpawnType.FungleShipStatus => MapNames.Fungle,
				_ => MapNames.Skeld
			};
		}

		/// <summary>Real host OR Menu "Unlock all" - BaneMenu gates only, not game AmHost.</summary>
		public static bool IsHostEffective()
		{
			if (BaneMenu.modules.DevUnlock.Enabled) return true;
			return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
		}

		public static bool IsAnticheatPresent()
		{
			if (BaneMenu.modules.DevUnlock.Enabled) return false;
			if(Constants.IsVersionModded() || PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return false;

			// On Freeplay, local, and modded lobbies, NetworkedPlayerInfo net objects are owned by the host (-2)
			// On vanilla lobbies, NetworkedPlayerInfo net objects are owned by the backend among us servers (-4)
			// If our NetworkedPlayerInfo net object is owned by the host, we can assume that the lobby has a lax anticheat without server authority
			// which does not require us to use any sort of bypasses
			return PlayerControl.LocalPlayer.Data.OwnerId != (int)OwnerIds.Host;
		}

		public static string GetPlayerColor(NetworkedPlayerInfo player)
		{
			int colorId = player.DefaultOutfit.ColorId;

			if(colorId < 0 || colorId >= Palette.ColorNames.Length)
			{
				return "Fortegreen";
			}

			return player.GetPlayerColorString();
		}

		// This kick method allows a player who is not the host of the lobby to kick someone out of the lobby by making them trigger the Among Us Anticheat
		// There are various RPCs that can only be sent by the host of the lobby, such as MurderPlayer, Shapeshift, ProtectPlayer, etc
		// These RPCs are sent by the host in response to their client-authoritative equivalent, such as CheckMurder, CheckShapeshift, CheckProtect, etc
		// If we are able to make a player send a host-only RPC without being the host of the lobby, we can make the anticheat kick them out of the lobby
		// Most RPC handlers have checks to ensure that the client is the host of the lobby to avoid exactly this exploit
		// however one exception is UpdateSystem RPC for Ventilation System
		// Sending a ventilation system update with an operation of StartCleaning or BootFromVent will result in the host sending a BootFromVent RPC, which is one of these host-only RPCs
		// however nowhere in the callstack from ShipStatus::UpdateSystem to PlayerPhysics::RpcBootFromVent is there a check to ensure that the current client is the host of the lobby
		// If you were to send this system update to someone other than the host, they will send the BootFromVent RPC and get kicked by the Among Us anticheat
		// In my experience this has been incredibly useful to kick out players who are blatantly hacking, calling useless meetings, or causing other mischief even if I am not the host if the lobby
		/// <summary>
		/// Official host ban (shows "[player] was banned by [host]" to the lobby).
		/// </summary>
		public static void HostBanPlayer(PlayerControl player)
		{
			if (player == null || AmongUsClient.Instance == null) return;
			if (!Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Ban", "You must be host for a normal ban.", 3);
				return;
			}
			AmongUsClient.Instance.KickPlayer(player.OwnerId, true);
		}

		/// <summary>
		/// Ventilation AC exploit kick - works as non-host, and as host without the public ban message.
		/// </summary>
		public static void AttemptBan(PlayerControl player, bool skipFirstStage = false)
		{
			if (player == null) return;

			// Lobby: VoteKick leave-rejoin loop (works on host too)
			if (BaneMenu.modules.troll.LobbyVoteKickLoop.IsLobby())
			{
				BaneMenu.modules.troll.LobbyVoteKickLoop.KickTarget(player);
				return;
			}

			// In-game: cannot use vent-kick on host
			if (AmongUsClient.Instance != null && player.OwnerId == AmongUsClient.Instance.HostId)
			{
				Bane.notifications.Send("Attempt to Ban", "You can only kick the host in the lobby (VoteKick loop).", 5);
				return;
			}

			KickPlayer(player, skipFirstStage, forceExploit: true);
		}

		public static void KickPlayer(PlayerControl player, bool skipFirstStage = false, bool forceExploit = false)
		{
			if(Utilities.IsHostEffective() && !forceExploit)
			{
				AmongUsClient.Instance.KickPlayer(player.OwnerId, true);
				return;
			}

			if(player.OwnerId == AmongUsClient.Instance.HostId)
			{
				return;
			}

			if(ShipStatus.Instance == null)
			{
				return;
			}

			if(!IsAnticheatPresent())
			{
				return;
			}

			// Always target Enter+Boot at the victim only (never broadcast Enter globally).
			// Global Enter was detectable by host AC when Kick All was used.
			MessageWriter enterVent = MessageWriter.Get(SendOption.None);
			enterVent.Write((ushort)0);
			enterVent.Write((byte)VentilationSystem.Operation.Enter);
			enterVent.Write((byte)0);

			MessageWriter bootFromVent = MessageWriter.Get(SendOption.None);
			bootFromVent.Write((ushort)1);
			bootFromVent.Write((byte)VentilationSystem.Operation.BootImpostors);
			bootFromVent.Write((byte)0);

			BatchedMessage batch = new BatchedMessage(player.OwnerId);
			batch.QueueUpdateSystem(PlayerControl.LocalPlayer, SystemTypes.Ventilation, enterVent);
			batch.QueueUpdateSystem(PlayerControl.LocalPlayer, SystemTypes.Ventilation, bootFromVent);
			batch.FinishBatch();

			enterVent.Recycle();
			bootFromVent.Recycle();
		}
	
		public static int GetVotekickCount(VoteBanSystem system, int clientId)
		{
			if(system == null || system.Votes == null) return 0;
			if(!system.Votes.TryGetValue(clientId, out Il2CppStructArray<int> votes) || votes == null) return 0;

			int voteCount = 0;
			foreach(int voter in votes)
			{
				if(voter != 0) voteCount++;
			}

			return voteCount;
		}

}
}