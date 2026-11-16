using BaneMenu.modules;
using BaneMenu.modules.misc;
using System;
using System.Text;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class ChatSection : Section
	{
		public ChatSection() : base("Chat") { }

		int unicodeLen = 16;
		string customMessage = "";
		string rtlBase = "";
		string quickChatIdText = "0";
		static readonly System.Random Rng = new System.Random();

		public override void Render()
		{
			// xN count is shared by all "xN" buttons - always visible at top
			GUILayout.Label("--- Send count (xN) ---");
			GUILayout.Label($"Times: {ModuleManager.chatMultiply.Times}x");
			ModuleManager.chatMultiply.Times = (int)Math.Round(Controls.HorizontalSlider(ModuleManager.chatMultiply.Times, 1, 20));
			ModuleManager.chatMultiply.TrueBatch = Controls.Toggle(ModuleManager.chatMultiply.TrueBatch, "True batch (one GameData packet)");

			GUILayout.Space(6);
			GUILayout.Label("--- Quick Chat (by ID) ---");
			GUILayout.Label($"Send Quick Chat By ID: {(StringNames)QuickChat.SelectedId}");
			bool qcCommit;
			quickChatIdText = Controls.TextBox("Number", quickChatIdText, "chat_quick_id", out qcCommit, 8);
			if (!string.IsNullOrEmpty(quickChatIdText) && ushort.TryParse(quickChatIdText, out ushort parsedId))
				QuickChat.SelectedId = parsedId;
			GUILayout.BeginHorizontal();
			if (Controls.Button("Send Red Text"))
				QuickChat.SendRedChat(QuickChat.SelectedId);
			if (Controls.Button("Send Big Text"))
				QuickChat.SendBigChat(QuickChat.SelectedId);
			GUILayout.EndHorizontal();

			GUILayout.Space(6);
			GUILayout.Label("--- Send ---");
			if (Controls.Button("Send blank chat"))
				ChatMultiply.SendBlank();
			if (Controls.Button($"Send blank chat x{ModuleManager.chatMultiply.Times}"))
				ChatMultiply.SendBlankMultiplied();

			GUILayout.Label("Custom message:");
			bool commit;
			customMessage = Controls.TextBox("Message", customMessage, "chat_custom_msg", out commit, 256);
			if (commit && !string.IsNullOrEmpty(customMessage))
				ChatMultiply.SendText(customMessage);
			if (Controls.Button("Send message"))
			{
				if (!string.IsNullOrEmpty(customMessage))
					ChatMultiply.SendText(customMessage);
			}
			if (Controls.Button($"Send message x{ModuleManager.chatMultiply.Times}"))
			{
				if (!string.IsNullOrEmpty(customMessage))
					ChatMultiply.SendTextMultiplied(customMessage);
			}

			GUILayout.Space(6);
			GUILayout.Label("--- Multiply (normal chat box) ---");
			ModuleManager.chatMultiply.Enabled = Controls.Toggle(ModuleManager.chatMultiply.Enabled, "Chat multiply enabled");
			GUILayout.Label("When enabled, each normal in-game chat send is repeated xN (slider above).");

			GUILayout.Space(6);
			GUILayout.Label("--- Unicode / random ---");
			GUILayout.Label("Random length: " + unicodeLen);
			unicodeLen = (int)Math.Round(Controls.HorizontalSlider(unicodeLen, 1, 64));
			if (Controls.Button("Send random BMP unicode"))
				ChatMultiply.SendText(RandomBmp(unicodeLen));
			if (Controls.Button($"Send random BMP unicode x{ModuleManager.chatMultiply.Times}"))
				ChatMultiply.SendTextMultiplied(RandomBmp(unicodeLen));
			if (Controls.Button("Send random emoji-ish"))
				ChatMultiply.SendText(RandomEmojiBlock(unicodeLen));
			if (Controls.Button("Send zalgo-ish"))
				ChatMultiply.SendText(Zalgo("chat", 8));
			if (Controls.Button("Send zero-width flood"))
				ChatMultiply.SendText(new string('\u200B', Math.Max(1, unicodeLen)));

			GUILayout.Label("RTL base text:");
			rtlBase = Controls.TextBox("RTL base", rtlBase, "chat_rtl_base", out _, 128);
			if (Controls.Button("Send RTL mark + text"))
			{
				string baseT = string.IsNullOrEmpty(rtlBase) ? "test" : rtlBase;
				ChatMultiply.SendText("\u202E" + baseT);
			}
			if (Controls.Button("Send null-byte in middle"))
				ChatMultiply.SendText("ab\0cd");

			GUILayout.Space(6);
			GUILayout.Label("--- Spam presets ---");
			if (Controls.Button($"Spam '.' x{ModuleManager.chatMultiply.Times}"))
				ChatMultiply.SendTextMultiplied(".");
			if (Controls.Button($"Spam space x{ModuleManager.chatMultiply.Times}"))
				ChatMultiply.SendTextMultiplied(" ");
			if (Controls.Button($"Spam newline x{ModuleManager.chatMultiply.Times}"))
				ChatMultiply.SendTextMultiplied("\n");
			if (Controls.Button($"Spam long AAAA x{ModuleManager.chatMultiply.Times}"))
				ChatMultiply.SendTextMultiplied(new string('A', 64));
			if (Controls.Button("Preset: hi"))
				ChatMultiply.SendText("hi");
			if (Controls.Button("Preset: gg"))
				ChatMultiply.SendText("gg");

			GUILayout.Space(6);
			GUILayout.Label("--- Notes ---");
			if (Controls.Button("Chat note DidVote"))
				ChatMultiply.SendText("[DidVote]");
			if (Controls.Button($"Chat note DidVote x{ModuleManager.chatMultiply.Times}"))
				ChatMultiply.SendTextMultiplied("[DidVote]");
		}

		static string RandomBmp(int len)
		{
			var sb = new StringBuilder(len);
			for (int i = 0; i < len; i++)
				sb.Append((char)Rng.Next(0x20, 0x7F));
			return sb.ToString();
		}

		static string RandomEmojiBlock(int len)
		{
			string[] pool = { "😀", "🔥", "💀", "✨", "🚀", "👀", "💯" };
			var sb = new StringBuilder();
			for (int i = 0; i < Math.Max(1, len / 2); i++)
				sb.Append(pool[Rng.Next(pool.Length)]);
			return sb.ToString();
		}

		static string Zalgo(string baseText, int intensity)
		{
			string marks = "̴̵̶̷̸̡̢̧̨̛̖̗̘̙̜̝̞̟̠̣̤̥̦̩̪̫̬̭̮̯̰̱̲̳̹̺̻̼͇͈͉͍͎̀́̂̃̄̅̆̇̈̉̊̋̌̍̎̏̐̑̒̓̔̽̾̿̀́͂̓̈́͆͊͋͌̕̚ͅ ͏͓͔͕͖͙͚͐͑͒͗͛ͣͤͥͦͧͨͩͪͫͬͭͮͯ͘͜͟͢͝͞͠͡";
			var sb = new StringBuilder();
			foreach (char c in baseText)
			{
				sb.Append(c);
				int n = Rng.Next(1, intensity + 1);
				for (int i = 0; i < n; i++)
					sb.Append(marks[Rng.Next(marks.Length)]);
			}
			return sb.ToString();
		}
	}
}
