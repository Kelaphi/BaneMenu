using HarmonyLib;
using Hazel;
using InnerNet;
using System;
using System.Text;
using UnityEngine;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// Logs IN/OUT RPCs to BepInEx. Skips movement spam (SnapTo) and empty noise by default.
	/// For reverse-engineering ban / AC / host packets.
	/// </summary>
	internal class RpcLogger : Module
	{
		public RpcLogger() : base("RPC Logger") { }

		public bool LogOutgoing { get; set; } = true;
		public bool LogIncoming { get; set; } = true;
		public bool SkipSnapTo { get; set; } = true;
		public bool SkipPet { get; set; } = true;
		public bool DumpHex { get; set; } = true;
		public int MaxHex { get; set; } = 48;

		static RpcLogger Instance => ModuleManager.rpcLogger;

		static string RpcName(byte id)
		{
			try { return ((RpcCalls)id).ToString(); }
			catch { return id.ToString(); }
		}

		static string Hex(MessageReader r, int max)
		{
			if (r == null) return "";
			try
			{
				int pos = r.Position;
				int left = r.BytesRemaining;
				int n = Math.Min(left, max);
				if (n <= 0) return "hex=";
				var sb = new StringBuilder("hex=");
				for (int i = 0; i < n; i++)
					sb.Append(r.ReadByte().ToString("X2")).Append(' ');
				r.Position = pos;
				if (left > max) sb.Append("...");
				return sb.ToString();
			}
			catch
			{
				return "hex=?";
			}
		}

		static string ObjInfo(InnerNetObject obj)
		{
			if (obj == null) return "obj=null";
			try
			{
				var sb = new StringBuilder();
				sb.Append("type=").Append(obj.GetType().Name);
				sb.Append(" netId=").Append(obj.NetId);
				sb.Append(" owner=").Append(obj.OwnerId);
				var pc = obj.TryCast<PlayerControl>();
				if (pc != null)
				{
					sb.Append(" pid=").Append(pc.PlayerId);
					if (pc.Data != null) sb.Append(" name=").Append(pc.Data.PlayerName);
				}
				return sb.ToString();
			}
			catch (Exception e)
			{
				return "obj_err=" + e.Message;
			}
		}

		static bool ShouldSkip(byte callId)
		{
			var inst = Instance;
			if (inst != null && inst.SkipSnapTo && callId == (byte)RpcCalls.SnapTo) return true;
			// Pet / CancelPet ids vary by version - skip by name when possible
			if (inst != null && inst.SkipPet)
			{
				try
				{
					string n = ((RpcCalls)callId).ToString();
					if (n.IndexOf("Pet", StringComparison.OrdinalIgnoreCase) >= 0) return true;
				}
				catch { }
			}
			return false;
		}

		static void LogLine(string dir, string body)
		{
			Bane.Log.LogInfo("[RPC " + dir + "] " + body);
		}

		static void LogIn(InnerNetObject obj, byte callId, MessageReader reader)
		{
			var inst = Instance;
			if (inst == null || !inst.Enabled || !inst.LogIncoming) return;
			if (ShouldSkip(callId)) return;
			var sb = new StringBuilder();
			sb.Append("rpc=").Append(RpcName(callId)).Append('(').Append(callId).Append(')');
			sb.Append(" | ").Append(ObjInfo(obj));
			if (inst.DumpHex && reader != null)
				sb.Append(" | ").Append(Hex(reader, inst.MaxHex));
			try
			{
				sb.Append(" | me=").Append(AmongUsClient.Instance != null ? AmongUsClient.Instance.ClientId.ToString() : "?");
				sb.Append(" host=").Append(AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost);
			}
			catch { }
			LogLine("IN", sb.ToString());
		}

		static void LogOut(uint targetNetId, byte callId, int targetClientId, SendOption option)
		{
			var inst = Instance;
			if (inst == null || !inst.Enabled || !inst.LogOutgoing) return;
			if (ShouldSkip(callId)) return;
			var sb = new StringBuilder();
			sb.Append("rpc=").Append(RpcName(callId)).Append('(').Append(callId).Append(')');
			sb.Append(" targetNetId=").Append(targetNetId);
			sb.Append(" targetClient=").Append(targetClientId);
			sb.Append(" opt=").Append(option);
			try
			{
				sb.Append(" me=").Append(AmongUsClient.Instance.ClientId);
				sb.Append(" AmHost=").Append(AmongUsClient.Instance.AmHost);
			}
			catch { }
			LogLine("OUT", sb.ToString());
		}

		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.StartRpcImmediately))]
		static class PatchOut
		{
			static void Postfix(uint targetNetId, byte callId, SendOption option, int targetClientId)
			{
				try { LogOut(targetNetId, callId, targetClientId, option); }
				catch { }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		static class PatchInPc
		{
			static void Prefix(PlayerControl __instance, byte callId, MessageReader reader)
			{
				try { LogIn(__instance, callId, reader); }
				catch { }
			}
		}

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
		static class PatchInCnt
		{
			static void Prefix(CustomNetworkTransform __instance, byte callId, MessageReader reader)
			{
				try { LogIn(__instance, callId, reader); }
				catch { }
			}
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
		static class PatchInShip
		{
			static void Prefix(ShipStatus __instance, byte callId, MessageReader reader)
			{
				try { LogIn(__instance, callId, reader); }
				catch { }
			}
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
		static class PatchInMeeting
		{
			static void Prefix(MeetingHud __instance, byte callId, MessageReader reader)
			{
				try { LogIn(__instance, callId, reader); }
				catch { }
			}
		}

		[HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.HandleRpc))]
		static class PatchInVoteBan
		{
			static void Prefix(VoteBanSystem __instance, byte callId, MessageReader reader)
			{
				try { LogIn(__instance, callId, reader); }
				catch { }
			}
		}
	}
}
