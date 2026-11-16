using Hazel;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.modules.troll
{
	/// <summary>
	/// Client-sided fake sabotages/doors aimed at a single client (Hydralum/Malum Schizo).
	/// Does not affect host or other players when client id resolves correctly.
	/// </summary>
	internal static class Schizo
	{
		public static void FakeReactor(PlayerControl target)
		{
			byte mapId = SafeMapId();
			SystemTypes sys = mapId == 2 ? SystemTypes.Laboratory
				: mapId == 4 ? SystemTypes.HeliSabotage
				: SystemTypes.Reactor;
			SendSabotage(target, sys, 128);
		}

		public static void FakeOxygen(PlayerControl target) => SendSabotage(target, SystemTypes.LifeSupp, 128);
		public static void FakeComms(PlayerControl target) => SendSabotage(target, SystemTypes.Comms, 128);
		public static void FakeMushroom(PlayerControl target) => SendSabotage(target, SystemTypes.MushroomMixupSabotage, 1);

		public static void CloseAllDoors(PlayerControl target)
		{
			if (!Valid(target) || ShipStatus.Instance.AllDoors == null) return;
			var seen = new HashSet<SystemTypes>();
			foreach (OpenableDoor door in ShipStatus.Instance.AllDoors)
			{
				if (door == null || !seen.Add(door.Room)) continue;
				CloseDoor(target, door.Room);
			}
		}

		public static void CloseDoor(PlayerControl target, SystemTypes room)
		{
			if (!Valid(target)) return;
			int clientId = ResolveClientId(target);
			if (clientId < 0 || IsHostClient(clientId)) return;

			MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
				ShipStatus.Instance.NetId, (byte)RpcCalls.CloseDoorsOfType, SendOption.Reliable, clientId);
			writer.Write((byte)room);
			AmongUsClient.Instance.FinishRpcImmediately(writer);
		}

		public static bool Valid(PlayerControl target) =>
			target != null && target.Data != null && ShipStatus.Instance != null
			&& PlayerControl.LocalPlayer != null && AmongUsClient.Instance != null;

		public static int ResolveClientId(PlayerControl target)
		{
			try
			{
				int id = AmongUsClient.Instance.GetClientIdFromCharacter(target);
				if (id >= 0) return id;
			}
			catch { }
			try { return target.Data != null ? target.Data.ClientId : -1; }
			catch { return -1; }
		}

		public static bool IsHostClient(int clientId)
		{
			try { return AmongUsClient.Instance != null && clientId == AmongUsClient.Instance.HostId; }
			catch { return false; }
		}

		public static bool IsHost(PlayerControl player)
		{
			if (player == null) return false;
			try { return IsHostClient(AmongUsClient.Instance.GetClientIdFromCharacter(player)); }
			catch { return false; }
		}

		static byte SafeMapId()
		{
			try { return GameOptionsManager.Instance.currentGameOptions.MapId; }
			catch { return 0; }
		}

		static void SendSabotage(PlayerControl target, SystemTypes sys, byte value)
		{
			if (!Valid(target)) return;
			int clientId = ResolveClientId(target);
			if (clientId < 0 || IsHostClient(clientId)) return;

			MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
				ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, clientId);
			writer.Write((byte)sys);
			writer.WritePacked(PlayerControl.LocalPlayer.NetId);
			writer.Write(value);
			AmongUsClient.Instance.FinishRpcImmediately(writer);
		}
	}
}
