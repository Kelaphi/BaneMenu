using Hazel;
using InnerNet;

namespace BaneMenu.modules.misc
{
	/// <summary>
	/// AuraMenu quick-chat sender by StringNames / ushort id
	/// (AuraCheats.SendRedChat / SendBigChat).
	/// </summary>
	internal static class QuickChat
	{
		public static ushort SelectedId = 0;

		public static void SendRedChat(ushort id)
		{
			if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null) return;

			MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
				PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SendQuickChat, SendOption.Reliable);
			writer.Write((byte)3);
			writer.Write((ushort)78);
			writer.Write((byte)1);
			writer.Write((byte)2);
			writer.Write(id);
			AmongUsClient.Instance.FinishRpcImmediately(writer);

			// Echo to self so we see the message (Aura: cuts local chat CD 6→3)
			MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(
				PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SendQuickChat, SendOption.Reliable,
				PlayerControl.LocalPlayer.OwnerId);
			writer2.Write((byte)3);
			writer2.Write((ushort)78);
			writer2.Write((byte)1);
			writer2.Write((byte)2);
			writer2.Write(id);
			AmongUsClient.Instance.FinishRpcImmediately(writer2);
		}

		public static void SendBigChat(ushort id)
		{
			if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null) return;

			MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
				PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SendQuickChat, SendOption.Reliable);
			writer.Write((byte)3);
			writer.Write((ushort)2276);
			writer.Write((byte)3);
			writer.Write((byte)2);
			writer.Write(id);
			writer.Write((byte)2);
			writer.Write(id);
			writer.Write((byte)2);
			writer.Write(id);
			AmongUsClient.Instance.FinishRpcImmediately(writer);

			MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(
				PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.SendQuickChat, SendOption.Reliable,
				PlayerControl.LocalPlayer.OwnerId);
			writer2.Write((byte)3);
			writer2.Write((ushort)2276);
			writer2.Write((byte)3);
			writer2.Write((byte)2);
			writer2.Write(id);
			writer2.Write((byte)2);
			writer2.Write(id);
			writer2.Write((byte)2);
			writer2.Write(id);
			AmongUsClient.Instance.FinishRpcImmediately(writer2);
		}
	}
}
