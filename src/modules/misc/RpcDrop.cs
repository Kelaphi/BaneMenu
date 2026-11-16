using HarmonyLib;
using Hazel;
using InnerNet;
using System.Collections.Generic;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// Drop outbound and/or inbound RpcCalls by call id.
	/// OUT: StartRpcImmediately never runs.
	/// IN: HandleRpc Prefix returns false on common net objects.
	/// </summary>
	internal class RpcDrop : Module
	{
		public RpcDrop() : base("RPC Drop") { }

		static readonly HashSet<byte> DroppedOut = new HashSet<byte>();
		static readonly HashSet<byte> DroppedIn = new HashSet<byte>();

		public static int CountOut => DroppedOut.Count;
		public static int CountIn => DroppedIn.Count;
		public static int Count => DroppedOut.Count + DroppedIn.Count;

		public static bool IsDroppedOut(RpcCalls call) => DroppedOut.Contains((byte)call);
		public static bool IsDroppedIn(RpcCalls call) => DroppedIn.Contains((byte)call);

		public static void SetDroppedOut(RpcCalls call, bool drop)
		{
			byte id = (byte)call;
			if (drop) DroppedOut.Add(id);
			else DroppedOut.Remove(id);
			Bane.Log.LogInfo($"RpcDrop OUT: {(drop ? "DROP" : "allow")} {call} ({id})");
		}

		public static void SetDroppedIn(RpcCalls call, bool drop)
		{
			byte id = (byte)call;
			if (drop) DroppedIn.Add(id);
			else DroppedIn.Remove(id);
			Bane.Log.LogInfo($"RpcDrop IN: {(drop ? "DROP" : "allow")} {call} ({id})");
		}

		public static void ClearAll()
		{
			DroppedOut.Clear();
			DroppedIn.Clear();
			Bane.Log.LogInfo("RpcDrop: cleared all OUT+IN");
		}

		static bool ShouldDropIn(byte callId) => DroppedIn.Contains(callId);

		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpcImmediately))]
		static class PatchStartRpc
		{
			static bool Prefix(
				InnerNetClient __instance,
				uint targetNetId,
				byte callId,
				SendOption option,
				int targetClientId,
				ref MessageWriter __result)
			{
				if (!DroppedOut.Contains(callId)) return true;

				string name;
				try { name = ((RpcCalls)callId).ToString(); }
				catch { name = callId.ToString(); }
				Bane.Log.LogInfo($"RpcDrop: blocked OUT {name} net={targetNetId}");

				try
				{
					__result = MessageWriter.Get(option);
					__result.StartMessage(6);
					__result.Write(__instance.GameId);
					__result.StartMessage(2);
					__result.WritePacked(targetNetId);
					__result.Write(callId);
					__result.EndMessage();
					__result.EndMessage();
				}
				catch
				{
					try { __result = MessageWriter.Get(option); } catch { __result = null; }
				}
				return false;
			}
		}

		static bool InPrefix(InnerNetObject obj, byte callId)
		{
			if (!ShouldDropIn(callId)) return true;
			string name;
			try { name = ((RpcCalls)callId).ToString(); }
			catch { name = callId.ToString(); }
			string objName = obj != null ? obj.GetType().Name : "?";
			Bane.Log.LogInfo($"RpcDrop: blocked IN {name} on {objName}");
			return false;
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		static class PatchInPc
		{
			static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
				=> InPrefix(__instance, callId);
		}

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
		static class PatchInCnt
		{
			static bool Prefix(CustomNetworkTransform __instance, byte callId, MessageReader reader)
				=> InPrefix(__instance, callId);
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
		static class PatchInShip
		{
			static bool Prefix(ShipStatus __instance, byte callId, MessageReader reader)
				=> InPrefix(__instance, callId);
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
		static class PatchInMeeting
		{
			static bool Prefix(MeetingHud __instance, byte callId, MessageReader reader)
				=> InPrefix(__instance, callId);
		}

		[HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.HandleRpc))]
		static class PatchInVoteBan
		{
			static bool Prefix(VoteBanSystem __instance, byte callId, MessageReader reader)
				=> InPrefix(__instance, callId);
		}
	}
}
