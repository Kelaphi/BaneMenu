using BaneMenu.network;
using Hazel;
using InnerNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace BaneMenu.ui.sections
{
	/// <summary>
	/// Lobby mass-DC research using non-spawn / non-despawn / non-host-role paths only.
	/// Weird payloads on normally "client-ish" RPCs, floods, extra bytes, own-netId variants.
	/// </summary>
	internal class DebugSection : Section
	{
		static readonly string[] SubNames = { "General", "RPC" };
		readonly Section rpc = new RpcSection();
		int subTab;

		public DebugSection() : base("Debug") { }

		public override void HandleSubsectionMove(int offset)
		{
			subTab = Mathf.Clamp(subTab + offset, 0, SubNames.Length - 1);
			BaneGui.ClosePopup();
			BaneGui.Unfocus();
		}

		public static bool ArmOnJoin;
		public static float ArmDelay = 1f;
		public static bool LogProbes = true;
		public static int SpamN = 10;
		public static string Last = "(none)";
		static bool joinBusy;
		static readonly HashSet<int> ArmedIds = new HashSet<int>();

		const int ProbeCount = 36;
		static readonly string[] ProbeNames =
		{
			"00 CheckName empty",
			"01 CheckName only spaces",
			"02 CheckName 1 char",
			"03 CheckName 200 As",
			"04 CheckName unicode Zalgo",
			"05 CheckName null byte in middle",
			"06 CheckName emoji flood",
			"07 CheckColor 255",
			"08 CheckColor 0 spam xN",
			"09 CheckColor every id 0-255 once",
			"10 SendChat empty",
			"11 SendChat 1 char",
			"12 SendChat 512 As",
			"13 SendChat newlines only",
			"14 SendChat unicode",
			"15 SendChat rapid xN",
			"16 CheckName rapid xN",
			"17 Own netId StartMeeting (no params)",
			"18 Own netId CloseMeeting",
			"19 Own netId Exiled",
			"20 Own netId ReportDeadBody 255",
			"21 Own netId CastVote 255/255",
			"22 Own netId ClearVote",
			"23 Own netId SetScanner true",
			"24 Own netId PlayAnimation 0",
			"25 Own netId EnterVent 0",
			"26 Own netId ExitVent 0",
			"27 Own netId unknown callId 250",
			"28 Own netId unknown callId 200",
			"29 Extra trailing bytes on CheckName",
			"30 Extra trailing bytes on Chat",
			"31 SendOption None CheckName",
			"32 SendOption None Chat",
			"33 ClientReady xN",
			"34 CheckName then Chat alternating xN",
			"35 Own CNT netId SnapTo 0,0 (seq weird)"
		};

		public override void Render()
		{
			int prev = subTab;
			subTab = Controls.SubTabs(SubNames, subTab, "debug_subtabs");
			if (subTab != prev) { BaneGui.ClosePopup(); BaneGui.Unfocus(); }
			GUILayout.Space(4);
			if (subTab == 1) { rpc.Render(); return; }

			GUILayout.Label("Debug - test tools + client-ish RPC probes");
			if (Utilities.IsHostEffective())
			{
				GUILayout.Label("--- Server-side player ---");
				GUILayout.BeginHorizontal();
				if (Controls.Button("Despawn Self (server)"))
				{
					try
					{
						var pc = PlayerControl.LocalPlayer;
						if (pc == null || AmongUsClient.Instance == null)
						{
							Bane.notifications.Send("Debug", "No local player.", 3);
						}
						else
						{
							InnerNetObject obj = pc.Cast<InnerNetObject>();
							AmongUsClient.Instance.Despawn(obj);
							Bane.notifications.Send("Debug", "Despawned local player (server).", 3);
							Last = "Despawn self";
						}
					}
					catch (Exception e)
					{
						Bane.notifications.Send("Debug", "Despawn failed: " + e.Message, 4);
					}
				}
				if (Controls.Button("Respawn Self (server)"))
				{
					try
					{
						var pc = PlayerControl.LocalPlayer;
						if (pc == null || AmongUsClient.Instance == null)
						{
							Bane.notifications.Send("Debug", "No local player.", 3);
						}
						else
						{
							AmongUsClient.Instance.Spawn(pc, pc.OwnerId, SpawnFlags.IsClientCharacter);
							Bane.notifications.Send("Debug", "Respawned local player (server).", 3);
							Last = "Respawn self";
						}
					}
					catch (Exception e)
					{
						try
						{
							var pc = PlayerControl.LocalPlayer;
							if (pc != null)
							{
								AmongUsClient.Instance.Spawn(pc);
								Bane.notifications.Send("Debug", "Respawned local player (fallback Spawn).", 3);
								Last = "Respawn self (fallback)";
							}
						}
						catch (Exception e2)
						{
							Bane.notifications.Send("Debug", "Respawn failed: " + e2.Message, 4);
						}
					}
				}
				GUILayout.EndHorizontal();
				GUILayout.Label("Host-auth / unlocked BaneMenu gates only.");
				GUILayout.Space(6);
			}

			GUILayout.Label("Weird payloads / floods on client-ish RPCs only");
			ArmOnJoin = Controls.Toggle(ArmOnJoin, "ARM ON JOIN");
			GUILayout.Label($"Delay: {ArmDelay:0.0}s");
			ArmDelay = Controls.HorizontalSlider(ArmDelay, 0f, 5f);
			LogProbes = Controls.Toggle(LogProbes, "Log");
			GUILayout.Label($"Spam N: {SpamN}");
			SpamN = Mathf.RoundToInt(Controls.HorizontalSlider(SpamN, 1, 50));
			GUILayout.Label("Last: " + Last);

			GUILayout.BeginHorizontal();
			if (Controls.Button("Fire armed")) FireArmed();
			if (Controls.Button("Arm all")) { for (int i = 0; i < ProbeCount; i++) ArmedIds.Add(i); }
			if (Controls.Button("Clear")) ArmedIds.Clear();
			GUILayout.EndHorizontal();

			// No nested ScrollView - outer MainUI feature pane scrolls the whole tab
			for (int i = 0; i < ProbeCount; i++)
			{
				GUILayout.BeginHorizontal();
				bool a = ArmedIds.Contains(i);
				bool n = Controls.TogglePill(a, "probe_" + i);
				if (n != a) { if (n) ArmedIds.Add(i); else ArmedIds.Remove(i); }
				if (Controls.Button(ProbeNames[i])) RunProbe(i);
				GUILayout.EndHorizontal();
			}
			GUILayout.Space(24f); // keep last row fully visible above scrollbar edge
		}

		public static void OnLocalJoinedLobby()
		{
			if (!ArmOnJoin || joinBusy || AmongUsClient.Instance == null) return;
			joinBusy = true;
			try { AmongUsClient.Instance.StartCoroutine(JoinRoutine().WrapToIl2Cpp()); }
			catch { joinBusy = false; }
		}

		static IEnumerator JoinRoutine()
		{
			if (ArmDelay > 0f) yield return new WaitForSecondsRealtime(ArmDelay);
			Note("ARM JOIN");
			FireArmed();
			joinBusy = false;
		}

		static void FireArmed()
		{
			foreach (int id in new List<int>(ArmedIds))
			{
				try { RunProbe(id); }
				catch (Exception e) { Note("ex " + id + ": " + e.Message); }
			}
		}

		static void Note(string m)
		{
			Last = m;
			if (LogProbes) Bane.Log.LogInfo("[Debug] " + m);
			Bane.notifications.Send("Debug", m, 2);
		}

		static uint LocalNet()
		{
			if (PlayerControl.LocalPlayer != null) return PlayerControl.LocalPlayer.NetId;
			return 0;
		}

		static uint LocalCntNet()
		{
			if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.NetTransform != null)
				return PlayerControl.LocalPlayer.NetTransform.NetId;
			return LocalNet();
		}

		static void Rpc(uint netId, byte call, SendOption opt, Action<MessageWriter> write = null)
		{
			if (AmongUsClient.Instance == null || netId == 0) throw new Exception("no net");
			MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(netId, call, opt, -1);
			write?.Invoke(w);
			AmongUsClient.Instance.FinishRpcImmediately(w);
		}

		public static void RunProbe(int id)
		{
			if (AmongUsClient.Instance == null) { Note("no client"); return; }
			uint me = LocalNet();

			switch (id)
			{
				case 0: CheckName(""); Note("CheckName empty"); break;
				case 1: CheckName("   "); Note("CheckName spaces"); break;
				case 2: CheckName("A"); Note("CheckName 1"); break;
				case 3: CheckName(new string('A', 200)); Note("CheckName 200"); break;
				case 4: CheckName("̴̡̛͍̖͇͚́͒̆͘"); Note("CheckName zalgo"); break;
				case 5: CheckName("ab\0cd"); Note("CheckName nullbyte"); break;
				case 6: CheckName("\U0001F480\U0001F480\U0001F480\U0001F480\U0001F480"); Note("CheckName emoji"); break;
				case 7: CheckColor(255); Note("CheckColor 255"); break;
				case 8:
					for (int i = 0; i < SpamN; i++) CheckColor(0);
					Note("CheckColor 0 x" + SpamN);
					break;
				case 9:
					for (int i = 0; i < 256; i++) CheckColor((byte)i);
					Note("CheckColor 0-255");
					break;
				case 10: Chat(""); Note("Chat empty"); break;
				case 11: Chat("."); Note("Chat 1"); break;
				case 12: Chat(new string('A', 512)); Note("Chat 512"); break;
				case 13: Chat("\n\n\n\n\n"); Note("Chat newlines"); break;
				case 14: Chat("🔥🔥🔥"); Note("Chat unicode"); break;
				case 15:
					for (int i = 0; i < SpamN; i++) Chat("x" + i);
					Note("Chat x" + SpamN);
					break;
				case 16:
					for (int i = 0; i < SpamN; i++) CheckName("n" + i);
					Note("CheckName x" + SpamN);
					break;
				case 17: Rpc(me, (byte)RpcCalls.StartMeeting, SendOption.Reliable); Note("own StartMeeting"); break;
				case 18: Rpc(me, (byte)RpcCalls.CloseMeeting, SendOption.Reliable); Note("own CloseMeeting"); break;
				case 19: Rpc(me, (byte)RpcCalls.Exiled, SendOption.Reliable); Note("own Exiled"); break;
				case 20:
					Rpc(me, (byte)RpcCalls.ReportDeadBody, SendOption.Reliable, w => w.Write((byte)255));
					Note("own Report 255");
					break;
				case 21:
					Rpc(me, (byte)RpcCalls.CastVote, SendOption.Reliable, w => { w.Write((byte)255); w.Write((byte)255); });
					Note("own CastVote");
					break;
				case 22: Rpc(me, (byte)RpcCalls.ClearVote, SendOption.Reliable); Note("own ClearVote"); break;
				case 23:
					Rpc(me, (byte)RpcCalls.SetScanner, SendOption.Reliable, w => { w.Write(true); w.Write((byte)1); });
					Note("own SetScanner");
					break;
				case 24:
					Rpc(me, (byte)RpcCalls.PlayAnimation, SendOption.Reliable, w => w.Write((byte)0));
					Note("own PlayAnimation 0");
					break;
				case 25:
					Rpc(me, (byte)RpcCalls.EnterVent, SendOption.Reliable, w => w.WritePacked(0));
					Note("own EnterVent 0");
					break;
				case 26:
					Rpc(me, (byte)RpcCalls.ExitVent, SendOption.Reliable, w => w.WritePacked(0));
					Note("own ExitVent 0");
					break;
				case 27: Rpc(me, 250, SendOption.Reliable); Note("own callId 250"); break;
				case 28: Rpc(me, 200, SendOption.Reliable); Note("own callId 200"); break;
				case 29:
					Rpc(me, (byte)RpcCalls.CheckName, SendOption.Reliable, w =>
					{
						w.Write("test");
						w.Write((byte)0xFF);
						w.Write((byte)0xFF);
						w.WritePacked(99999);
					});
					Note("CheckName + trailing");
					break;
				case 30:
					Rpc(me, (byte)RpcCalls.SendChat, SendOption.Reliable, w =>
					{
						w.Write("hi");
						w.Write(new string('B', 100));
					});
					Note("Chat + trailing");
					break;
				case 31: Rpc(me, (byte)RpcCalls.CheckName, SendOption.None, w => w.Write("none")); Note("CheckName None"); break;
				case 32: Rpc(me, (byte)RpcCalls.SendChat, SendOption.None, w => w.Write("nonechat")); Note("Chat None"); break;
				case 33:
					for (int i = 0; i < SpamN; i++)
					{
						try
						{
							var b = new BatchedMessage();
							b.QueueClientReady(AmongUsClient.Instance.ClientId);
							b.FinishBatch();
						}
						catch { }
					}
					Note("ClientReady x" + SpamN);
					break;
				case 34:
					for (int i = 0; i < SpamN; i++)
					{
						CheckName("a" + i);
						Chat("b" + i);
					}
					Note("alt name/chat x" + SpamN);
					break;
				case 35:
					Rpc(LocalCntNet(), (byte)RpcCalls.SnapTo, SendOption.Reliable, w =>
					{
						NetHelpers.WriteVector2(new Vector2(0f, 0f), w);
						w.Write((ushort)65535);
					});
					Note("own CNT SnapTo seq 65535");
					break;
				default: Note("bad id"); break;
			}
		}

		static void CheckName(string name)
		{
			uint me = LocalNet();
			if (me == 0) throw new Exception("no LP");
			Rpc(me, (byte)RpcCalls.CheckName, SendOption.Reliable, w => w.Write(name ?? ""));
		}

		static void CheckColor(byte c)
		{
			uint me = LocalNet();
			if (me == 0) throw new Exception("no LP");
			Rpc(me, (byte)RpcCalls.CheckColor, SendOption.Reliable, w => w.Write(c));
		}

		static void Chat(string t)
		{
			uint me = LocalNet();
			if (me == 0) throw new Exception("no LP");
			// Prefer batched chat if available; else raw RPC
			try
			{
				if (PlayerControl.LocalPlayer != null)
				{
					var b = new BatchedMessage();
					b.QueueSendChat(PlayerControl.LocalPlayer, t ?? "");
					b.FinishBatch();
					return;
				}
			}
			catch { }
			Rpc(me, (byte)RpcCalls.SendChat, SendOption.Reliable, w => w.Write(t ?? ""));
		}
	}
}
