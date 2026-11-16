using System;
using AmongUs.GameOptions;
using BaneMenu.network;
using Hazel;
using InnerNet;
using UnityEngine;

namespace BaneMenu.modules.host
{
	/// <summary>
	/// Dummy / control logic from AuraMenu HostCheats + PlayersTab spawn path.
	/// </summary>
	internal static class HostDummies
	{
		// From AuraMenu CheatToggles / PlayersLists
		public static bool controlAll;
		public static bool controlDummies;
		public static float moveSpeed = 0.02f;
		public static byte chasedPlayerId = 255;

		// AuraMenu CheatToggles.optionsBypass
		public static bool optionsBypass;

		private static float moveDelay;

		// Free PlayerId for a new dummy (never an existing player's id).
		public static byte AllocateFreePlayerId()
		{
			bool[] used = new bool[256];
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc == null) continue;
				used[pc.PlayerId] = true;
			}
			for (int i = 0; i < 250; i++)
			{
				if (!used[i]) return (byte)i;
			}
			return 255;
		}

		public static PlayerControl AddVisualDummy(byte colorId = 0, string name = "Dummy", string hatId = "", string skinId = "", string petId = "", string visorId = "", string namePlateId = "", uint level = 0, string friendCode = null, string puid = null)
		{
			if (AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null) return null;
			byte dummyId = AllocateFreePlayerId();
			if (dummyId == 255) return null;
			return AddDummy(dummyId, PlayerControl.LocalPlayer.transform.position, colorId, name, hatId, skinId, petId, visorId, namePlateId, level, friendCode, puid);
		}

		/// <summary>
		/// Custom dummy: free PlayerId + Aura AddPlayerInfo(AddDummy) + Clone-Player Spawn body + RpcSet*.
		/// No BatchedMessage.QueueSpawn - that is what triggers public anticheat bans.
		/// </summary>
		public static PlayerControl AddDummy(byte playerId, Vector2 pos, byte colorId = 0, string name = "Dummy", string hatId = "", string skinId = "", string petId = "", string visorId = "", string namePlateId = "", uint level = 0, string friendCode = null, string puid = null)
		{
			if (AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null) return null;
			if (playerId == PlayerControl.LocalPlayer.PlayerId) return null;

			string safeName = string.IsNullOrEmpty(name) ? "Dummy" : name;
			hatId = hatId ?? "";
			skinId = skinId ?? "";
			petId = petId ?? "";
			visorId = visorId ?? "";
			namePlateId = namePlateId ?? "";

			// Body: same as Clone Player / Aura PlayersTab (Instantiate + Spawn only)
			PlayerControl dummy = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, pos, Quaternion.identity);
			dummy.PlayerId = playerId;

			// Aura HostCheats - local PlayerInfo for this dummy (do NOT QueueSpawn it; AC flags that)
			try
			{
				if (GameData.Instance != null)
					GameData.Instance.AddPlayerInfo(GameData.Instance.AddDummy(dummy));
			}
			catch { }

			AmongUsClient.Instance.Spawn(dummy);

			// Cosmetics only via normal player RPCs (Aura HostCheats sequence)
			dummy.RpcSetColor(colorId);
			dummy.RpcSetName(safeName);
			dummy.RpcSetHat(hatId);
			dummy.RpcSetSkin(skinId);
			dummy.RpcSetPet(petId);
			dummy.RpcSetVisor(visorId);
			dummy.RpcSetNamePlate(namePlateId);
			dummy.RpcSetLevel(level);

			try
			{
				if (dummy.Data != null)
				{
					if (!string.IsNullOrEmpty(friendCode))
					{
						try { dummy.Data.FriendCode = friendCode; } catch { }
					}
					if (!string.IsNullOrEmpty(puid))
					{
						try { dummy.Data.Puid = puid; } catch { }
					}
				}
			}
			catch { }

			try { dummy.NetTransform.enabled = true; } catch { }
			return dummy;
		}

		// EXACT: AuraMenu PlayersTab "Spawn Dummy" / "Clone Player" button (clones target PlayerId on network)
		// Optional spawnPos: when provided (e.g. local player position), spawns the body there instead of at target.
		public static PlayerControl SpawnDummyAtPlayer(PlayerControl target, Vector2? spawnPos = null)
		{
			if (AmongUsClient.Instance == null || target == null) return null;

			Vector2 pos = spawnPos ?? (Vector2)target.transform.position;
			PlayerControl pc = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, pos, Quaternion.identity);
			pc.PlayerId = target.PlayerId;
			AmongUsClient.Instance.Spawn(pc);
			return pc;
		}

		/// <summary>
		/// Host-tab custom dummy: free id + networked PlayerInfo + Clone-Player body Spawn.
		/// </summary>
		public static PlayerControl SpawnCustomDummyAt(
			PlayerControl basePlayer,
			byte colorId = 0,
			string name = "Dummy",
			string hatId = "",
			string skinId = "",
			string petId = "",
			string visorId = "",
			string namePlateId = "",
			uint level = 0,
			string friendCode = null,
			string puid = null)
		{
			return AddVisualDummy(colorId, name, hatId, skinId, petId, visorId, namePlateId, level, friendCode, puid);
		}

		// EXACT: AuraMenu HostCheats.DespawnDummy
		public static void DespawnDummy(PlayerControl target)
		{
			try
			{
				InnerNetObject obj = target.Cast<InnerNetObject>();
				if (obj != null) AmongUsClient.Instance.Despawn(obj);

				UnityEngine.Object.Destroy(target.gameObject);
			}

			catch { }
		}

		public static PlayerControl ClonePlayer(PlayerControl target)
		{
			if (target == null || target.Data == null) return null;

			NetworkedPlayerInfo info = target.Data;
			var outfit = info.DefaultOutfit;

			byte colorId = (byte)outfit.ColorId;
			string name = info.PlayerName ?? "Clone";
			string hatId = outfit.HatId ?? "";
			string skinId = outfit.SkinId ?? "";
			string petId = outfit.PetId ?? "";
			string visorId = outfit.VisorId ?? "";
			string namePlateId = outfit.NamePlateId ?? "";
			uint level = info.PlayerLevel;

			return AddVisualDummy(colorId, name, hatId, skinId, petId, visorId, namePlateId, level);
		}

		// EXACT: AuraMenu PlayerActions.KickMeeting
		public static void KickMeeting(PlayerControl target)
		{
			if (MeetingHud.Instance == null || target == null) return;

			BatchedMessage batch = new BatchedMessage(target.OwnerId);

			MeetingHud.VoterState[] votes = Array.Empty<MeetingHud.VoterState>();

			batch.QueueVotingComplete(votes, null, false, false, 0);

			batch.QueueCloseMeeting();
			batch.FinishBatch();
		}

		// EXACT: AuraMenu Utils.SendDataflag
		public static void SendDataflag(MessageWriter writer, uint netId)
		{
			MessageWriter msg = MessageWriter.Get(SendOption.Reliable);
			msg.StartMessage(5);
			msg.Write(AmongUsClient.Instance.GameId);

			msg.StartMessage(1);
			msg.WritePacked(netId);
			msg.Write(writer, false);
			msg.EndMessage();

			msg.EndMessage();
			AmongUsClient.Instance.SendOrDisconnect(msg);
			msg.Recycle();
		}

		public static PlayerControl GetPlayerFromId(byte id)
		{
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc != null && pc.PlayerId == id) return pc;
			}
			return null;
		}

		// EXACT: AuraMenu HostCheats.ControlPlayers
		public static void ControlPlayers()
		{
			if ((!controlAll && !controlDummies) || !Utilities.IsHostEffective() || PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.NetTransform == null) return;

			if (Time.time < moveDelay) return;

			PlayerControl target = PlayerControl.LocalPlayer;

			if (GetPlayerFromId(chasedPlayerId) != null) target = GetPlayerFromId(chasedPlayerId);

			CustomNetworkTransform net = target.NetTransform;

			if (target == PlayerControl.LocalPlayer)
			{
				chasedPlayerId = PlayerControl.LocalPlayer.PlayerId;

				if (net.isPaused || net.sendQueue.Count == 0) return;

				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (pc == null || pc.NetTransform == null || pc == PlayerControl.LocalPlayer) continue;

					bool isDummy = AmongUsClient.Instance.GetClientFromCharacter(pc) == null;

					if ((isDummy && !controlDummies) || (!isDummy && !controlAll)) continue;

					MessageWriter writer = MessageWriter.Get(SendOption.Reliable);

					writer.Write(net.lastSequenceId);
					ushort num = (ushort)net.sendQueue.Count;
					writer.WritePacked(num);
					foreach (Vector2 item in net.sendQueue)
					{
						NetHelpers.WriteVector2(item, writer);
					}

					SendDataflag(writer, pc.NetTransform.NetId);
					writer.Recycle();

					if (isDummy) pc.transform.position = PlayerControl.LocalPlayer.transform.position;

					net.lastSequenceId++;
				}
			}
			else
			{
				if (net.isPaused || net.incomingPosQueue.Count == 0) return;

				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (pc == null || pc.NetTransform == null || pc == target) continue;

					bool isDummy = AmongUsClient.Instance.GetClientFromCharacter(pc) == null;

					if ((isDummy && !controlDummies) || (!isDummy && !controlAll)) continue;

					MessageWriter writer = MessageWriter.Get(SendOption.Reliable);

					writer.Write(net.lastSequenceId);
					ushort num = (ushort)net.incomingPosQueue.Count;
					writer.WritePacked(num);
					foreach (Vector2 item in net.incomingPosQueue)
					{
						NetHelpers.WriteVector2(item, writer);
					}

					SendDataflag(writer, pc.NetTransform.NetId);
					writer.Recycle();

					if (isDummy) pc.transform.position = target.transform.position;
				}
			}

			moveDelay = Time.time + 0.10f + (moveSpeed * PlayerControl.AllPlayerControls.Count);
		}

		// EXACT: AuraMenu HostOnlyTab.UpdatePlayers
		public static void UpdatePlayers(int targetPlayers)
		{
			if (GameOptionsManager.Instance == null) return;

			try
			{
				bool isHnS = false;
				try
				{
					isHnS = GameOptionsManager.Instance.currentGameOptions != null
						&& GameOptionsManager.Instance.currentGameOptions.GameMode == GameModes.HideNSeek;
				}
				catch { }

				if (isHnS)
				{
					var options = GameOptionsManager.Instance?.currentHideNSeekGameOptions;
					if (options != null) options.MaxPlayers = targetPlayers;
				}
				else
				{
					var options = GameOptionsManager.Instance?.currentNormalGameOptions;
					if (options != null) options.MaxPlayers = targetPlayers;
				}
			}
			catch { }
		}
	}
}
