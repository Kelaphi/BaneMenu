using HarmonyLib;
using Hazel;
using InnerNet;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.modules.visuals
{
	/// <summary>
	/// Detect other BaneMenu users via custom RPC. Tag is "BANE USER" in cardinal red.
	/// Only players who sent a valid handshake are tagged - never inferred.
	/// </summary>
	internal class ModDetection : Module
	{
		/// <summary>Custom call id - must not collide with vanilla RpcCalls.</summary>
		public const byte RpcId = 211;
		const string Magic = "BaneMenu";

		public static readonly Color TagColor = new Color(0.77f, 0.12f, 0.24f, 1f);

		public ModDetection() : base("ModDetection")
		{
			/* default off until config */ base.Enabled = false; // Always on
		}

		static ModDetection Instance => ModuleManager.modDetection;

		/// <summary>ClientIds that sent a valid BaneMenu handshake.</summary>
		readonly HashSet<int> _baneClients = new HashSet<int>();
		int _rpcCooldown;

		public static string TagRich =>
			$"<b><color=#{ColorUtility.ToHtmlStringRGB(TagColor)}>BANE USER</color></b>";

		public bool IsBaneUser(PlayerControl pc)
		{
			if (pc == null) return false;
			int cid = ClientIdOf(pc);
			return cid >= 0 && _baneClients.Contains(cid);
		}

		public bool IsBaneUser(byte playerId)
		{
			PlayerControl pc = null;
			try
			{
				if (PlayerControl.AllPlayerControls != null)
				{
					foreach (PlayerControl p in PlayerControl.AllPlayerControls)
					{
						if (p != null && p.PlayerId == playerId) { pc = p; break; }
					}
				}
			}
			catch { }
			return IsBaneUser(pc);
		}

		public bool TryGetMod(byte playerId, out string label)
		{
			label = null;
			if (!IsBaneUser(playerId)) return false;
			label = "BANE USER";
			return true;
		}

		public int Count => _baneClients.Count;

		static int ClientIdOf(PlayerControl pc)
		{
			if (pc == null || AmongUsClient.Instance == null) return -1;
			try
			{
				int id = AmongUsClient.Instance.GetClientIdFromCharacter(pc);
				if (id >= 0) return id;
			}
			catch { }
			try
			{
				if (pc.Data != null) return pc.Data.ClientId;
			}
			catch { }
			return -1;
		}

		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Update))]
		class BroadcastPatch
		{
			static void Postfix()
			{
				if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null) return;
				var state = AmongUsClient.Instance.GameState;
				if (state != InnerNetClient.GameStates.Joined && state != InnerNetClient.GameStates.Started)
					return;

				if (Instance._rpcCooldown > 0) { Instance._rpcCooldown--; return; }

				try
				{
					MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
						PlayerControl.LocalPlayer.NetId, RpcId, SendOption.Reliable, -1);
					writer.Write(Magic);
					writer.Write(MyPluginInfo.PLUGIN_VERSION ?? "1.0.0");
					AmongUsClient.Instance.FinishRpcImmediately(writer);
					Instance._rpcCooldown = Mathf.Max(20, (int)(0.75f * Application.targetFrameRate));
				}
				catch { }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		class ReceivePatch
		{
			static void Postfix(PlayerControl __instance, byte callId, MessageReader reader)
			{
				if (__instance == null) return;
				if (callId != RpcId) return;
				if (reader == null) return;

				try
				{
					int pos = reader.Position;
					if (reader.BytesRemaining < 2) { reader.Position = pos; return; }

					string tag = reader.ReadString();
					// Strict magic check - never tag on garbage / other mods
					if (tag != Magic)
					{
						reader.Position = pos;
						return;
					}
					if (reader.BytesRemaining > 0)
						reader.ReadString(); // version (ignored)

					int cid = ClientIdOf(__instance);
					if (cid >= 0)
						Instance._baneClients.Add(cid);

					reader.Position = pos;
				}
				catch { }
			}
		}

		/// <summary>
		/// Apply / strip tag after other name logic (RevealRoles, colorblind, etc.).
		/// </summary>
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
		[HarmonyPriority(Priority.Last)]
		class NameTagPatch
		{
			static void Postfix(PlayerControl __instance)
			{
				if (__instance == null || __instance.Data == null) return;
				if (__instance.cosmetics == null || __instance.cosmetics.nameText == null) return;

				var tmp = __instance.cosmetics.nameText;
				string cur = tmp.text ?? "";
				bool show = Instance.IsBaneUser(__instance); // always-on feature

				if (!show)
				{
					if (cur.IndexOf("BANE USER") >= 0)
					{
						try { tmp.text = StripTag(cur); } catch { }
					}
					return;
				}

				try
				{
					tmp.richText = true;
					string body = StripTag(cur);
					if (string.IsNullOrEmpty(body))
						body = __instance.Data.PlayerName ?? "";
					// Force tag on top every frame so RevealRoles cannot erase it
					string next = TagRich + "\n" + body;
					if (cur != next)
						tmp.text = next;
				}
				catch { }
			}
		}

		static string StripTag(string text)
		{
			if (string.IsNullOrEmpty(text)) return "";
			string[] lines = text.Replace("\r", "").Split('\n');
			var kept = new List<string>();
			for (int i = 0; i < lines.Length; i++)
			{
				if (lines[i].IndexOf("BANE USER") >= 0) continue;
				kept.Add(lines[i]);
			}
			return string.Join("\n", kept).Trim();
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
		class PlayerLeft
		{
			static void Postfix(ClientData data)
			{
				try
				{
					if (data != null)
						Instance._baneClients.Remove(data.Id);
				}
				catch { }
			}
		}

		protected override void OnDisable()
		{
			// Feature is always on - ignore disable attempts
			/* default off until config */ base.Enabled = false;
		}
	}
}
