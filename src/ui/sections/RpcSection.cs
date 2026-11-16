using BaneMenu.modules.misc;
using Hazel;
using InnerNet;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	/// <summary>
	/// Testing tab: pick any RpcCalls entry, fill params (players by name), send.
	/// No GUILayout.TextField - IL2CPP unstripping breaks it.
	/// </summary>
	internal class RpcSection : Section
	{
		public RpcSection() : base("RPC") { }

		private enum ParamKind
		{
			None,
			Byte,
			SByte,
			Int,
			UInt,
			UShort,
			Bool,
			Float,
			Player,   // writes packed NetId of selected PlayerControl
			PlayerId, // writes byte PlayerId
			Vector2,
			RoleType,
			MurderFlags
		}

		private class ParamDef
		{
			public string Label;
			public ParamKind Kind;
			public float Min, Max;
			public float FloatVal;
			public int IntVal;
			public bool BoolVal;
			public PlayerControl PlayerVal;
			public float FloatVal2; // second component for Vector2
		}

		private class RpcDef
		{
			public string Name;
			public RpcCalls Call;
			public ParamDef[] Params;
			/// <summary>If true, net object is chosen player "Source"; else LocalPlayer.</summary>
			public bool NeedsSourcePlayer;
		}

		private static readonly List<RpcDef> Catalog = BuildCatalog();

		private RpcDef selected;
		private int sendMode = 0; // 0=their netId 1=my netId 2=GameDataTo them 3=my CNT for SnapTo
		private bool sendAsLocal = true;
		private PlayerControl sourcePlayer;

		// Root messages (not RpcCalls) - what host-auth probing often needs
		private enum RootMsg { None, StartGame, EndGame, WaitForHost }
		private RootMsg selectedRoot = RootMsg.None;

		private static List<RpcDef> BuildCatalog()
		{
			var list = new List<RpcDef>();

			void Add(string name, RpcCalls call, bool needsSource, params ParamDef[] ps)
			{
				list.Add(new RpcDef { Name = name, Call = call, NeedsSourcePlayer = needsSource, Params = ps ?? Array.Empty<ParamDef>() });
			}

			ParamDef Byte(string label, int def = 0, int max = 255) =>
				new ParamDef { Label = label, Kind = ParamKind.Byte, Min = 0, Max = max, IntVal = def };
			ParamDef SByte(string label, int def = 0) =>
				new ParamDef { Label = label, Kind = ParamKind.SByte, Min = -128, Max = 127, IntVal = def };
			ParamDef Int(string label, int def = 0, int min = -100, int max = 500) =>
				new ParamDef { Label = label, Kind = ParamKind.Int, Min = min, Max = max, IntVal = def };
			ParamDef UInt(string label, int def = 0) =>
				new ParamDef { Label = label, Kind = ParamKind.UInt, Min = 0, Max = 100, IntVal = def };
			ParamDef UShort(string label, int def = 0) =>
				new ParamDef { Label = label, Kind = ParamKind.UShort, Min = 0, Max = 65535, IntVal = def };
			ParamDef Bool(string label, bool def = false) =>
				new ParamDef { Label = label, Kind = ParamKind.Bool, BoolVal = def };
			ParamDef Flt(string label, float def = 0f, float min = -50f, float max = 50f) =>
				new ParamDef { Label = label, Kind = ParamKind.Float, Min = min, Max = max, FloatVal = def };
			ParamDef Player(string label) =>
				new ParamDef { Label = label, Kind = ParamKind.Player };
			ParamDef PlayerId(string label) =>
				new ParamDef { Label = label, Kind = ParamKind.PlayerId };
			ParamDef Vec2(string label) =>
				new ParamDef { Label = label, Kind = ParamKind.Vector2, Min = -50f, Max = 50f };
			ParamDef Role(string label) =>
				new ParamDef { Label = label, Kind = ParamKind.RoleType, Min = 0, Max = 20, IntVal = 0 };
			ParamDef Murder(string label) =>
				new ParamDef { Label = label, Kind = ParamKind.MurderFlags, Min = 0, Max = 7, IntVal = 1 };

			// Common / useful test RPCs
			Add("PlayAnimation", RpcCalls.PlayAnimation, false, Byte("Animation Id", 1));
			Add("CompleteTask", RpcCalls.CompleteTask, false, UInt("Task Id", 0));
			Add("Exiled", RpcCalls.Exiled, true);
			Add("CheckColor", RpcCalls.CheckColor, false, Byte("Color Id", 0, 17));
			Add("SetColor", RpcCalls.SetColor, true, Byte("Color Id", 0, 17));
			Add("ReportDeadBody", RpcCalls.ReportDeadBody, false, PlayerId("Body (or none=255)"));
			Add("MurderPlayer", RpcCalls.MurderPlayer, true, Player("Victim"), Murder("Flags"));
			Add("SetScanner", RpcCalls.SetScanner, true, Bool("Scanning", true), Byte("Seq", 1));
			Add("SendChatNote", RpcCalls.SendChatNote, true, PlayerId("Target PlayerId"), Byte("Note Type", 0));
			Add("SetStartCounter", RpcCalls.SetStartCounter, false, Int("Seq", 0, 0, 1000), SByte("Counter", 5));
			Add("EnterVent", RpcCalls.EnterVent, true, Int("Vent Id", 0, 0, 50));
			Add("ExitVent", RpcCalls.ExitVent, true, Int("Vent Id", 0, 0, 50));
			Add("SnapTo", RpcCalls.SnapTo, true, Vec2("Position"), UShort("Seq", 1));
			Add("CloseMeeting", RpcCalls.CloseMeeting, false);
			Add("VotingComplete", RpcCalls.VotingComplete, false, PlayerId("Exiled PlayerId 255=none"), Bool("Tie", false), Bool("WasTie?", false));
			Add("CastVote", RpcCalls.CastVote, false, PlayerId("Voter"), PlayerId("Suspect 255=skip"));
			Add("ClearVote", RpcCalls.ClearVote, false);
			Add("AddVote", RpcCalls.AddVote, false, PlayerId("Voter Client?"), PlayerId("Suspect"));
			Add("CloseDoorsOfType", RpcCalls.CloseDoorsOfType, false, Byte("System Type", 0));
			Add("ClimbLadder", RpcCalls.ClimbLadder, true, Byte("Ladder Id", 0), Byte("LastClimb", 0));
			Add("UsePlatform", RpcCalls.UsePlatform, true);
			Add("Pet", RpcCalls.Pet, true, Vec2("Pos"), Vec2("Pet Pos"));
			Add("CancelPet", RpcCalls.CancelPet, true);
			Add("SetRole", RpcCalls.SetRole, true, Role("Role"), Bool("Can Override", true));
			Add("ProtectPlayer", RpcCalls.ProtectPlayer, true, Player("Target"), Byte("Color Id", 0, 17));
			Add("Shapeshift", RpcCalls.Shapeshift, true, Player("Target"), Bool("Animate", true));
			Add("CheckMurder", RpcCalls.CheckMurder, false, Player("Target"));
			Add("CheckProtect", RpcCalls.CheckProtect, false, Player("Target"));
			Add("CheckShapeshift", RpcCalls.CheckShapeshift, false, Player("Target"), Bool("Should Animate", true));
			Add("RejectShapeshift", RpcCalls.RejectShapeshift, false);
			Add("CheckVanish", RpcCalls.CheckVanish, false, Flt("Max Duration", 10f, 0f, 30f));
			Add("StartVanish", RpcCalls.StartVanish, true);
			Add("CheckAppear", RpcCalls.CheckAppear, false, Bool("Should Animate", true));
			Add("StartAppear", RpcCalls.StartAppear, true, Bool("Should Animate", true));
			Add("UseZipline", RpcCalls.UseZipline, true, Bool("From Top", true));
			Add("TriggerSpores", RpcCalls.TriggerSpores, true);
			Add("CheckSpore", RpcCalls.CheckSpore, false, Byte("Mushroom Id", 0));
			Add("UpdateSystem", RpcCalls.UpdateSystem, false,
				Byte("SystemType", 0), Byte("Amount / Op", 0), Player("Source (optional)"));

			// Also dump every enum value as a no-param stub so nothing is missing
			foreach (RpcCalls call in Enum.GetValues(typeof(RpcCalls)))
			{
				string n = call.ToString();
				bool exists = false;
				foreach (var d in list)
				{
					if (d.Call == call) { exists = true; break; }
				}
				if (!exists)
					Add(n + " (raw/no params)", call, false);
			}

			list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			return list;
		}

		public override void Render()
		{
			if (RpcDrop.Count > 0)
			{
				GUILayout.Label($"Dropping OUT={RpcDrop.CountOut}  IN={RpcDrop.CountIn}");
				if (Controls.Button("Clear all drops"))
				{
					RpcDrop.ClearAll();
					Bane.notifications.Send("RPC Drop", "Cleared all drops", 2);
				}
				GUILayout.Space(4);
			}
			if (selectedRoot != RootMsg.None)
				RenderRootDetail();
			else if (selected == null)
				RenderList();
			else
				RenderDetail();
		}

		private void RenderRootDetail()
		{
			if (Controls.Button("< Back"))
			{
				selectedRoot = RootMsg.None;
				return;
			}

			GUILayout.Label($"Root message: {selectedRoot}");
			GUILayout.Label("These are NOT RpcCalls - they use AmongUsClient root sends.");
			GUILayout.Label($"AmHost = {AmongUsClient.Instance?.AmHost}");
			GUILayout.Label($"GameState = {AmongUsClient.Instance?.GameState}");

			if (selectedRoot == RootMsg.StartGame)
			{
				GUILayout.Label("Sends AmongUsClient.SendStartGame() once.");
				if (Controls.Button("SEND StartGame once"))
					TrySendStartGameSpam(1);
				if (Controls.Button("SEND StartGame x50"))
					TrySendStartGameSpam(50);
				if (Controls.Button("SEND StartGame x282 (temp-ban style)"))
					TrySendStartGameSpam(282);
			}
			else if (selectedRoot == RootMsg.EndGame)
			{
				if (Controls.Button("SEND EndGame"))
					TrySendEndGame();
			}
		}

		private void TrySendStartGameSpam(int count)
		{
			try
			{
				if (AmongUsClient.Instance == null)
				{
					Bane.notifications.Send("RPC", "Not connected.", 3);
					return;
				}

					Bane.Log.LogInfo($"RPC Tester: SendStartGame x{count} (AmHost={Utilities.IsHostEffective()})");
				for (int i = 0; i < count; i++)
					AmongUsClient.Instance.SendStartGame();

				Bane.notifications.Send("RPC", $"Sent StartGame x{count} (host={Utilities.IsHostEffective()})", 4);
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"StartGame send failed: {e}");
				Bane.notifications.Send("RPC", $"StartGame failed: {e.Message}", 5);
			}
		}

		private void TrySendEndGame()
		{
			try
			{
				if (GameManager.Instance == null)
				{
					Bane.notifications.Send("RPC", "No GameManager.", 3);
					return;
				}
				GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
				Bane.notifications.Send("RPC", "Sent RpcEndGame", 3);
			}
			catch (Exception e2)
			{
				Bane.Log.LogError($"EndGame failed: {e2}");
				Bane.notifications.Send("RPC", $"EndGame failed: {e2.Message}", 5);
			}
		}

		private void RenderList()
		{
			GUILayout.Label("RPC / Root Tester - probe what non-host can send");
			GUILayout.Label("Player params use lobby names, not raw ids.");

			GUILayout.Space(4);
			GUILayout.Label("--- Root messages (host-auth tests) ---");
			if (Controls.Button("StartGame  (root - temp-ban / start probe)"))
				selectedRoot = RootMsg.StartGame;
			if (Controls.Button("EndGame  (root)"))
				selectedRoot = RootMsg.EndGame;
			if (Controls.Button("Send StartGame x10  (spam probe)"))
			{
				TrySendStartGameSpam(10);
				return;
			}

			GUILayout.Space(4);
			GUILayout.Label("--- RpcCalls ---");

			// No nested ScrollView - outer MainUI feature pane scrolls the whole tab
			foreach (var def in Catalog)
			{
				string label = $"{def.Name}  [{(byte)def.Call}]";
				if (Controls.Button(label))
				{
					selected = def;
					// reset player picks
					foreach (var p in def.Params)
					{
						if (p.Kind == ParamKind.Player || p.Kind == ParamKind.PlayerId)
							p.PlayerVal = PlayerControl.LocalPlayer;
					}
					sourcePlayer = PlayerControl.LocalPlayer;
				}
			}
			GUILayout.Space(24f);
		}

		private void RenderDetail()
		{
			if (Controls.Button("< Back to RPC list"))
			{
				selected = null;
				return;
			}

			GUILayout.Label($"RPC: {selected.Name}  (id={(byte)selected.Call})");

			// No nested ScrollView - outer MainUI feature pane scrolls the whole tab
			GUILayout.Label("--- How to send (research) ---");
			GUILayout.Label("Mode 0: their PlayerControl netId (classic spoof - AC sees you)");
			GUILayout.Label("Mode 1: YOUR netId (honest sender; payload may still name them)");
			GUILayout.Label("Mode 2: GameDataTo only them (they alone get it; still from your client)");
			GUILayout.Label("Mode 3: their NetTransform netId (SnapTo-style objects)");
			sendMode = Mathf.RoundToInt(Controls.HorizontalSlider(sendMode, 0, 3));
			GUILayout.Label($"Send mode: {sendMode}");
			sendAsLocal = Controls.Toggle(sendAsLocal, "Force LocalPlayer as source object");
			if (!sendAsLocal)
			{
				GUILayout.Label("Target / 'as' player:");
				DrawPlayerPicker(ref sourcePlayer);
			}

			GUILayout.Space(4);
			GUILayout.Label("--- Parameters ---");
			if (selected.Params.Length == 0)
				GUILayout.Label("(no parameters)");

			foreach (var p in selected.Params)
				DrawParam(p);

			GUILayout.Space(8);
			bool dropOut = RpcDrop.IsDroppedOut(selected.Call);
			bool dropOutNew = Controls.Toggle(dropOut, "Drop OUT (block sending this RPC)");
			if (dropOutNew != dropOut)
			{
				RpcDrop.SetDroppedOut(selected.Call, dropOutNew);
				Bane.notifications.Send("RPC Drop", (dropOutNew ? "OUT drop " : "OUT allow ") + selected.Name, 3);
			}

			bool dropIn = RpcDrop.IsDroppedIn(selected.Call);
			bool dropInNew = Controls.Toggle(dropIn, "Drop IN (ignore receiving this RPC)");
			if (dropInNew != dropIn)
			{
				RpcDrop.SetDroppedIn(selected.Call, dropInNew);
				Bane.notifications.Send("RPC Drop", (dropInNew ? "IN drop " : "IN allow ") + selected.Name, 3);
			}

			if (dropOutNew)
				GUILayout.Label("Outbound " + selected.Name + " is swallowed.");
			if (dropInNew)
				GUILayout.Label("Inbound " + selected.Name + " is ignored on this client.");

			GUILayout.Space(4);
			if (Controls.Button("SEND RPC"))
				TrySend();
			GUILayout.Space(24f);
		}

		private void DrawParam(ParamDef p)
		{
			GUILayout.Label(p.Label);

			switch (p.Kind)
			{
				case ParamKind.Bool:
					p.BoolVal = Controls.Toggle(p.BoolVal, p.BoolVal ? "true" : "false");
					break;

				case ParamKind.Byte:
				case ParamKind.SByte:
				case ParamKind.Int:
				case ParamKind.UInt:
				case ParamKind.UShort:
				case ParamKind.RoleType:
				case ParamKind.MurderFlags:
					p.IntVal = Mathf.RoundToInt(Controls.HorizontalSlider(p.IntVal, p.Min, p.Max));
					GUILayout.Label($"= {p.IntVal}");
					break;

				case ParamKind.Float:
					p.FloatVal = Controls.HorizontalSlider(p.FloatVal, p.Min, p.Max);
					GUILayout.Label($"= {p.FloatVal:F2}");
					break;

				case ParamKind.Vector2:
					GUILayout.Label("X");
					p.FloatVal = Controls.HorizontalSlider(p.FloatVal, p.Min, p.Max);
					GUILayout.Label($"X = {p.FloatVal:F2}");
					GUILayout.Label("Y");
					p.FloatVal2 = Controls.HorizontalSlider(p.FloatVal2, p.Min, p.Max);
					GUILayout.Label($"Y = {p.FloatVal2:F2}");
					break;

				case ParamKind.Player:
				case ParamKind.PlayerId:
					DrawPlayerPicker(ref p.PlayerVal);
					if (p.Kind == ParamKind.PlayerId && p.PlayerVal != null && p.PlayerVal.Data != null)
						GUILayout.Label($"(PlayerId = {p.PlayerVal.PlayerId})");
					if (p.Kind == ParamKind.PlayerId)
					{
						if (Controls.Button("Set to 255 (none/button/skip)"))
							p.PlayerVal = null; // special: null means write 255
					}
					break;
			}
		}

		private void DrawPlayerPicker(ref PlayerControl current)
		{
			if (PlayerControl.AllPlayerControls == null || PlayerControl.AllPlayerControls.Count == 0)
			{
				GUILayout.Label("(no players)");
				return;
			}

			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc == null || pc.Data == null) continue;
				string name = pc.Data.PlayerName ?? $"#{pc.PlayerId}";
				bool isSel = current == pc;
				if (Controls.Toggle(isSel, $"{name} (id {pc.PlayerId})"))
				{
					if (!isSel) current = pc;
				}
			}
		}

		private void TrySend()
		{
			try
			{
				if (AmongUsClient.Instance == null)
				{
					Bane.notifications.Send("RPC", "Not connected.", 3);
					return;
				}

				PlayerControl source = sendAsLocal ? PlayerControl.LocalPlayer : sourcePlayer;
				if (source == null)
				{
					Bane.notifications.Send("RPC", "Pick a source player.", 3);
					return;
				}

				PlayerControl local = PlayerControl.LocalPlayer;
				uint netId;
				int targetClient = -1;
				string modeName;

				switch (sendMode)
				{
					case 1: // honest: your object
						if (local == null)
						{
							Bane.notifications.Send("RPC", "No LocalPlayer.", 3);
							return;
						}
						netId = local.NetId;
						if (selected.Call == RpcCalls.SnapTo && local.NetTransform != null)
							netId = local.NetTransform.NetId;
						modeName = "MY netId";
						break;
					case 2: // GameDataTo the chosen player's client only
						netId = source.NetId;
						if (selected.Call == RpcCalls.SnapTo && source.NetTransform != null)
							netId = source.NetTransform.NetId;
						targetClient = source.OwnerId;
						modeName = "GameDataTo their client";
						break;
					case 3: // their CNT net id
						if (source.NetTransform == null)
						{
							Bane.notifications.Send("RPC", "No NetTransform on source.", 3);
							return;
						}
						netId = source.NetTransform.NetId;
						modeName = "their NetTransform netId";
						break;
					default: // 0 classic spoof on their PlayerControl
						netId = source.NetId;
						if (selected.Call == RpcCalls.SnapTo && source.NetTransform != null)
							netId = source.NetTransform.NetId;
						modeName = "their PlayerControl netId";
						break;
				}

				MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
					netId,
					(byte)selected.Call,
					SendOption.Reliable,
					targetClient
				);

				WriteParams(writer, selected.Params);
				AmongUsClient.Instance.FinishRpcImmediately(writer);

				Bane.Log.LogInfo($"RPC Tester sent {selected.Name} ({(byte)selected.Call}) mode={modeName} netId={netId} targetClient={targetClient} refPlayer={source.Data?.PlayerName}");
				Bane.notifications.Send("RPC", $"Sent {selected.Name} [{modeName}]", 3);
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"RPC Tester send failed: {e}");
				Bane.notifications.Send("RPC", $"Send failed: {e.Message}", 5);
			}
		}

		private static void WriteParams(MessageWriter writer, ParamDef[] ps)
		{
			foreach (var p in ps)
			{
				switch (p.Kind)
				{
					case ParamKind.Byte:
						writer.Write((byte)Mathf.Clamp(p.IntVal, 0, 255));
						break;
					case ParamKind.SByte:
						writer.Write((sbyte)Mathf.Clamp(p.IntVal, -128, 127));
						break;
					case ParamKind.Int:
						writer.WritePacked(p.IntVal);
						break;
					case ParamKind.UInt:
						writer.WritePacked((uint)Math.Max(0, p.IntVal));
						break;
					case ParamKind.UShort:
						writer.Write((ushort)Mathf.Clamp(p.IntVal, 0, 65535));
						break;
					case ParamKind.Bool:
						writer.Write(p.BoolVal);
						break;
					case ParamKind.Float:
						writer.Write(p.FloatVal);
						break;
					case ParamKind.Vector2:
						// Prefer packed Among Us vector encoding when available
						try
						{
							NetHelpers.WriteVector2(new Vector2(p.FloatVal, p.FloatVal2), writer);
						}
						catch
						{
							writer.Write(p.FloatVal);
							writer.Write(p.FloatVal2);
						}
						break;
					case ParamKind.Player:
						if (p.PlayerVal != null)
							writer.WritePacked(p.PlayerVal.NetId);
						else
							writer.WritePacked(0);
						break;
					case ParamKind.PlayerId:
						if (p.PlayerVal == null)
							writer.Write((byte)255);
						else
							writer.Write(p.PlayerVal.PlayerId);
						break;
					case ParamKind.RoleType:
						writer.WritePacked(p.IntVal);
						break;
					case ParamKind.MurderFlags:
						writer.Write(p.IntVal);
						break;
				}
			}
		}
	}
}
