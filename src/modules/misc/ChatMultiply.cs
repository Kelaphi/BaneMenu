using HarmonyLib;
using BaneMenu.network;
using UnityEngine;

namespace BaneMenu.modules.misc
{
	internal class ChatMultiply : Module
	{
		public ChatMultiply() : base("Chat Multiply") { }

		public int Times { get; set; } = 2;
		/// <summary>If true, N chats go in one GameData packet.</summary>
		public bool TrueBatch { get; set; } = false;

		static bool reentrant;
		static ChatMultiply Instance => ModuleManager.chatMultiply;

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
		static class PatchRpcSendChat
		{
			static bool Prefix(PlayerControl __instance, string chatText)
			{
				if (reentrant) return true;
				if (Instance == null || !Instance.Enabled) return true;
				if (__instance != PlayerControl.LocalPlayer) return true;

				int n = Mathf.Clamp(Instance.Times, 1, 20);
				if (n <= 1) return true;

				reentrant = true;
				try
				{
					SendMulti(__instance, chatText ?? "", n);
				}
				finally
				{
					reentrant = false;
				}
				return false;
			}
		}

		static void SendMulti(PlayerControl source, string text, int n)
		{
			if (Instance != null && Instance.TrueBatch)
			{
				BatchedMessage batch = new BatchedMessage();
				for (int i = 0; i < n; i++)
					batch.QueueSendChat(source, text);
				batch.FinishBatch();
			}
			else
			{
				for (int i = 0; i < n; i++)
				{
					BatchedMessage batch = new BatchedMessage();
					batch.QueueSendChat(source, text);
					batch.FinishBatch();
				}
			}
		}

		public static void SendBlank() => SendText("");
		public static void SendBlankMultiplied() => SendTextMultiplied("");

		public static void SendText(string text)
		{
			PlayerControl lp = PlayerControl.LocalPlayer;
			if (lp == null)
			{
				Bane.notifications.Send("Chat", "No local player.", 2);
				return;
			}
			try
			{
				reentrant = true;
				BatchedMessage batch = new BatchedMessage();
				batch.QueueSendChat(lp, text ?? "");
				batch.FinishBatch();
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("Chat", e.Message, 3);
			}
			finally
			{
				reentrant = false;
			}
		}

		public static void SendTextMultiplied(string text)
		{
			PlayerControl lp = PlayerControl.LocalPlayer;
			if (lp == null)
			{
				Bane.notifications.Send("Chat", "No local player.", 2);
				return;
			}
			int n = Mathf.Clamp(Instance != null ? Instance.Times : 2, 1, 20);
			try
			{
				reentrant = true;
				SendMulti(lp, text ?? "", n);
				Bane.notifications.Send("Chat", $"Sent x{n}", 2);
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("Chat", e.Message, 3);
			}
			finally
			{
				reentrant = false;
			}
		}

		public static void SendNote(ChatNoteTypes note)
		{
			PlayerControl lp = PlayerControl.LocalPlayer;
			if (lp == null || lp.Data == null) return;
			try
			{
				BatchedMessage batch = new BatchedMessage();
				batch.QueueSendChatNote(lp, lp.PlayerId, note);
				batch.FinishBatch();
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("Chat", e.Message, 3);
			}
		}

		public static void SendNoteMultiplied(ChatNoteTypes note)
		{
			PlayerControl lp = PlayerControl.LocalPlayer;
			if (lp == null || lp.Data == null) return;
			int n = Mathf.Clamp(Instance != null ? Instance.Times : 2, 1, 20);
			try
			{
				if (Instance != null && Instance.TrueBatch)
				{
					BatchedMessage batch = new BatchedMessage();
					for (int i = 0; i < n; i++)
						batch.QueueSendChatNote(lp, lp.PlayerId, note);
					batch.FinishBatch();
				}
				else
				{
					for (int i = 0; i < n; i++)
						SendNote(note);
				}
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("Chat", e.Message, 3);
			}
		}
	}
}
