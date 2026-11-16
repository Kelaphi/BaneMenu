using BepInEx.Unity.IL2CPP.Utils.Collections;
using BaneMenu.modules;
using BaneMenu.network;
using InnerNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BaneMenu.ui.sections
{
	internal class HostSection : Section
	{
		static readonly string[] SubNames = { "General", "Roles" };
		readonly Section roles = new RolesSection();
		int subTab;

		public HostSection() : base("Host") { }

		public override void HandleSubsectionMove(int offset)
		{
			subTab = Mathf.Clamp(subTab + offset, 0, SubNames.Length - 1);
			BaneGui.ClosePopup();
			BaneGui.Unfocus();
		}

		private byte selectedMap = 0;
		private int selectedSpawnIndex = 0;
		public static bool SpawnPlayerNetworkOnly = false;

		static string ghostChatMessage = "hello";
		static string ghostChatName = "???";
		static int ghostChatSize = 100;
		static string ghostChatColor = "FF0000"; // RRGGBB
		private Controls.PlayerColors selectedColor = 0;

		static int customMaxPlayers = 15;

		public static readonly Queue<InnerNetObject> lobbyList = new Queue<InnerNetObject>();
		public static readonly Queue<InnerNetObject> shipList = new Queue<InnerNetObject>();

		public override void Render()
		{
			int prev = subTab;
			subTab = Controls.SubTabs(SubNames, subTab, "host_subtabs");
			if (subTab != prev) { BaneGui.ClosePopup(); BaneGui.Unfocus(); }
			GUILayout.Space(4);
			if (subTab == 1) { roles.Render(); return; }

			if(PlayerControl.LocalPlayer == null)
			{
				GUILayout.Label("You are not currently in a game, these options will not work.");
			}
			else if(!Utilities.IsHostEffective())
			{
				GUILayout.Label("You are not the host of the current lobby. Using these options will either do nothing or get you banned by the anticheat");
			}

			// ---- Control All / Control Dummies (Aura HostOnlyTab - top of Host) ----
			GUILayout.Space(4);
			GUILayout.Label("--- Control Players / Dummies ---");
			BaneMenu.modules.host.HostDummies.controlAll = Controls.Toggle(
				BaneMenu.modules.host.HostDummies.controlAll, "Control All Players");
			BaneMenu.modules.host.HostDummies.controlDummies = Controls.Toggle(
				BaneMenu.modules.host.HostDummies.controlDummies, "Control Dummies");
			GUILayout.Label($"Move Delay: {BaneMenu.modules.host.HostDummies.moveSpeed:F2}");
			BaneMenu.modules.host.HostDummies.moveSpeed = Controls.HorizontalSlider(
				BaneMenu.modules.host.HostDummies.moveSpeed, 0.01f, 0.1f);
			GUILayout.Label("Walk while enabled, or set Move All Players To on a player in Players tab.");

			GUILayout.Space(6);
			GUILayout.Label("--- Ghost Chat / AC Probe (top) ---");
			GUILayout.Label("Spawn off-map (100000,100000) → chat / probe → despawn");
			bool gCommitMsg, gCommitCol;
			ghostChatMessage = Controls.TextBox("Message", ghostChatMessage, "ghost_chat_msg", out gCommitMsg, 80);
			GUILayout.Label($"Font size: {ghostChatSize}");
			ghostChatSize = (int)Controls.HorizontalSlider(Mathf.Clamp(ghostChatSize, 10, 300), 10, 300);
			ghostChatColor = Controls.TextBox("Color RRGGBB", ghostChatColor, "ghost_chat_col", out gCommitCol, 6);
			if (Controls.Button("Send Ghost Chat"))
			{
				if (AmongUsClient.Instance != null)
					AmongUsClient.Instance.StartCoroutine(SendGhostChat().WrapToIl2Cpp());
			}
			if (Controls.Button("Ghost AC Probe (RPCs from fake NetId)"))
			{
				if (AmongUsClient.Instance != null)
					AmongUsClient.Instance.StartCoroutine(GhostAcProbe().WrapToIl2Cpp());
			}
			GUILayout.Space(6);

			{
				bool prevEveryone = ModuleManager.disableKillButton.Everyone;
				bool nextEveryone = Controls.Toggle(prevEveryone, "Disable Kill Button (everyone, NaN cooldown)");
				if (nextEveryone != prevEveryone)
				{
					ModuleManager.disableKillButton.Everyone = nextEveryone;
					if (nextEveryone)
						ModuleManager.disableKillButton.ApplyAllMarked();
					else if (Utilities.IsHostEffective() && PlayerControl.AllPlayerControls != null)
					{
						foreach (var p in PlayerControl.AllPlayerControls)
						{
							if (p == null || p.Data == null) continue;
							if (!ModuleManager.disableKillButton.Targets.Contains(p.PlayerId))
								BaneMenu.modules.host.DisableKillButton.RestoreDefault(p);
						}
					}
				}
			}

			ModuleManager.botDetector.Enabled = Controls.Toggle(ModuleManager.botDetector.Enabled, "Bot Detector (host bans / else leave)");

			ModuleManager.banMidGame.Enabled = Controls.Toggle(ModuleManager.banMidGame.Enabled, "Be able to ban players mid-game");

			ModuleManager.flipSkeld.Enabled = Controls.Toggle(ModuleManager.flipSkeld.Enabled, "Use Flipped Skeld Map");

			ModuleManager.disableGameEnd.Enabled = Controls.Toggle(ModuleManager.disableGameEnd.Enabled, "Disable Game End");
			ModuleManager.disableVentClean.Enabled = Controls.Toggle(ModuleManager.disableVentClean.Enabled, "Disable Vent Clean");
			ModuleManager.disableSporeTriggers.Enabled = Controls.Toggle(ModuleManager.disableSporeTriggers.Enabled, "Disable Spore Triggers");
			ModuleManager.disableZipline.Enabled = Controls.Toggle(ModuleManager.disableZipline.Enabled, "Disable Zipline");

			
			GUILayout.Label("Create lobby:");
			GUILayout.Label("If HostGame API fails: enable inject then use vanilla Create Game.");
			ModuleManager.smallLobby.Enabled = Controls.Toggle(ModuleManager.smallLobby.Enabled, "Inject MaxPlayers on next HostGame");

			// AuraMenu Options Bypass MaxPlayers (HostOnlyTab.UpdatePlayers) - 0–255
			BaneMenu.modules.host.HostDummies.optionsBypass = Controls.Toggle(
				BaneMenu.modules.host.HostDummies.optionsBypass, "Options Bypass");
			if (BaneMenu.modules.host.HostDummies.optionsBypass)
			{
				GUILayout.Label($"Max Players: {customMaxPlayers}");
				customMaxPlayers = (int)Controls.HorizontalSlider(customMaxPlayers, 0, 255);
				if (Controls.Button("Set Max Players"))
				{
					BaneMenu.modules.host.HostDummies.UpdatePlayers(customMaxPlayers);
					Bane.notifications.Send("Max Players", $"Options Bypass MaxPlayers = {customMaxPlayers}", 3);
				}
				GUILayout.BeginHorizontal();
				if (Controls.Button("Randomize Max"))
				{
					customMaxPlayers = UnityEngine.Random.Range(0, 255);
					BaneMenu.modules.host.HostDummies.UpdatePlayers(customMaxPlayers);
				}
				if (Controls.Button("Reset Max (15)"))
				{
					customMaxPlayers = 15;
					BaneMenu.modules.host.HostDummies.UpdatePlayers(customMaxPlayers);
				}
				GUILayout.EndHorizontal();
			}
			if (Controls.Button("Create Lobby w/ Max"))
			{
				BaneMenu.modules.host.SmallLobby.CreateLobby(Mathf.Clamp(customMaxPlayers, 1, 255));
			}

			GUILayout.BeginHorizontal();
			ModuleManager.blockLowLevels.Enabled = Controls.Toggle(ModuleManager.blockLowLevels.Enabled, $"Kick players with less than {ModuleManager.blockLowLevels.MinLevel} levels");
			ModuleManager.blockLowLevels.MinLevel = (uint)Controls.HorizontalSlider(ModuleManager.blockLowLevels.MinLevel, 0, 100);
			GUILayout.EndHorizontal();

			ModuleManager.alwaysAllowStart.Enabled = Controls.Toggle(ModuleManager.alwaysAllowStart.Enabled, "Always Allow Start Button");

			if(Controls.Button("Force Start Game"))
			{
				ForceStartGame();
			}

			if(Controls.Button("Kill Everyone"))
			{
				KillAllPlayers();
			}

			GUILayout.BeginHorizontal();
			if(Controls.Button("Force Crewmate Victory"))
			{
				// Just in case the user has this enabled
				ModuleManager.disableGameEnd.Enabled = false;

				GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask, false);
				Bane.notifications.Send("Game Finished", "You ended the game with a crewmate victory.", 5);
			}

			if(Controls.Button("Force Imposter Victory"))
			{
				// Just in case the user has this enabled
				ModuleManager.disableGameEnd.Enabled = false;

				GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
				Bane.notifications.Send("Game Finished", "You ended the game with an imposter victory.", 5);
			}
			GUILayout.EndHorizontal();

			if(Controls.Button("Temp Ban All Players"))
			{
				ModuleManager.tempBanAll.Enabled = true;
			}

			// Lobby-only: stack CoStartGame audio by spamming StartGame root message
			if(Controls.Button("Earrape (35x StartGame)"))
			{
				if (AmongUsClient.Instance == null)
				{
					Bane.notifications.Send("Earrape", "Not in a lobby.", 3);
				}
				else if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
				{
					Bane.notifications.Send("Earrape", "Use in lobby before the game starts.", 4);
				}
				else
				{
					for (int i = 0; i < 35; i++)
						AmongUsClient.Instance.SendStartGame();
					Bane.notifications.Send("Earrape", "Sent StartGame x35 (audio stack).", 4);
				}
			}

			GUILayout.Space(5);
			GUILayout.Label("Map Spawner:");

			GUILayout.Label($"Selected map: {(MapNames)selectedMap}");
			selectedMap = (byte)Controls.HorizontalSlider(selectedMap, 0, 5);

			GUILayout.BeginHorizontal();
			if(Controls.Button("Despawn Map"))
			{
				DespawnMap();
			}

			if(Controls.Button("Spawn Map"))
			{
				AmongUsClient.Instance.StartCoroutine(SpawnMap(selectedMap, false).WrapToIl2Cpp());
			}
			if(Controls.Button("Spawn Map at Me"))
			{
				AmongUsClient.Instance.StartCoroutine(SpawnMap(selectedMap, true).WrapToIl2Cpp());
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if(Controls.Button("Despawn Lobby"))
			{
				DespawnLobby();
			}

			if(Controls.Button("Spawn Lobby"))
			{
				SpawnLobby(false);
			}
			if(Controls.Button("Spawn Lobby at Me"))
			{
				SpawnLobby(true);
			}
			GUILayout.EndHorizontal();

			GUILayout.Space(5);
			GUILayout.Label("NonAddressable Spawnables:");
			int spawnCount = 0;
			string spawnLabel = "(none)";
			try
			{
				var spawnList = AmongUsClient.Instance != null ? AmongUsClient.Instance.NonAddressableSpawnableObjects : null;
				if (spawnList != null && spawnList.Count > 0)
				{
					spawnCount = spawnList.Count;
					selectedSpawnIndex = Mathf.Clamp(selectedSpawnIndex, 0, spawnCount - 1);
					var sel = spawnList[selectedSpawnIndex];
					if (sel != null)
					{
						string go = sel.gameObject != null ? sel.gameObject.name : "?";
						spawnLabel = string.Format("[{0}] {1} (SpawnId={2})", selectedSpawnIndex, go, sel.SpawnId);
					}
					else spawnLabel = string.Format("[{0}] null", selectedSpawnIndex);
				}
			}
			catch { spawnLabel = "(error reading list)"; }
			GUILayout.Label(spawnLabel);
			if (spawnCount >= 1)
				selectedSpawnIndex = (int)Controls.HorizontalSlider(selectedSpawnIndex, 0, Math.Max(0, spawnCount - 1));
			if(spawnCount >= 1 && Controls.Button("Spawn Selected"))
			{
				SpawnNonAddressable(selectedSpawnIndex, false);
			}
			if(spawnCount >= 1 && Controls.Button("Spawn Selected at Me"))
			{
				SpawnNonAddressable(selectedSpawnIndex, true);
			}
			if(spawnCount >= 1 && Controls.Button("Despawn All of Selected Type"))
			{
				DespawnAllOfSelectedType(selectedSpawnIndex);
			}
			GUILayout.BeginHorizontal();
			if(Controls.Button("Despawn All Players (keep me)"))
			{
				DespawnAllPlayers(keepLocal: true);
			}
			if(Controls.Button("Despawn ALL Players"))
			{
				DespawnAllPlayers(keepLocal: false);
			}
			GUILayout.EndHorizontal();
			SpawnPlayerNetworkOnly = Controls.Toggle(SpawnPlayerNetworkOnly, "Spawn/Orbit: network-only pos (debug)");


			if(Controls.Button("Spawn Player"))
			{
				AmongUsClient.Instance.StartCoroutine(SpawnPlayerAtMe().WrapToIl2Cpp());
			}
			if(Controls.Button("Spawn Player at NaN"))
			{
				AmongUsClient.Instance.StartCoroutine(SpawnPlayerAtNaN().WrapToIl2Cpp());
			}
			if(Controls.Button("Clone Me (Player + Info)"))
			{
				AmongUsClient.Instance.StartCoroutine(CloneMeAsDuplicate().WrapToIl2Cpp());
			}

			// ---- Dummy controls (custom spawn UI removed - it did not work reliably) ----
			GUILayout.Space(6);
			if (Controls.Button("Despawn All Dummies"))
			{
				int n = 0;
				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (pc == null) continue;
					if (AmongUsClient.Instance.GetClientFromCharacter(pc) == null)
					{
						BaneMenu.modules.host.HostDummies.DespawnDummy(pc);
						n++;
					}
				}
				Bane.notifications.Send("Dummy", $"Despawned {n} dummy(s)", 3);
			}

			GUILayout.Space(5);
			GUILayout.Label("Meeting Controls:");
			ModuleManager.disableMeetings.Enabled = Controls.Toggle(ModuleManager.disableMeetings.Enabled, "Disable Meetings");
			Bane.routines.reportBodySpam.Enabled = Controls.Toggle(Bane.routines.reportBodySpam.Enabled, "Spam Report Bodies");
			ModuleManager.voteImmune.Enabled = Controls.PlayerSpecificToggle("Vote Immune", PlayerControl.LocalPlayer, ModuleManager.voteImmune.targets);
			Bane.routines.voteSpammer.Enabled = Controls.GlobalPlayerSpecificToggle("Spam Votes", Bane.routines.voteSpammer.targets);

			if(Controls.Button("Close Meeting"))
			{
				CloseMeeting();
			}

			GUILayout.Space(5);
			GUILayout.Label("Shapeshift Controls:");
			if(Controls.Button("Shapeshift Everyone Into Me"))
			{
				AmongUsClient.Instance.StartCoroutine(ShapeshiftAll(PlayerControl.LocalPlayer).WrapToIl2Cpp());
			}

			if(Controls.Button("Shapeshift Everyone Into Random"))
			{
				PlayerControl target = Utilities.GetRandomPlayer(false, false, false, false);
				AmongUsClient.Instance.StartCoroutine(ShapeshiftAll(target).WrapToIl2Cpp());
			}

			if(Controls.Button("Revert All Shapeshifts"))
			{
				AmongUsClient.Instance.StartCoroutine(RevertAllShapeshift().WrapToIl2Cpp());
			}

			GUILayout.Space(5);
			GUILayout.Label("Color Controls:");

			GUILayout.Label($"Change everyone's color to: {selectedColor}");
			selectedColor = Controls.HorizontalColorSlider(selectedColor);

			if(Controls.Button("Change Colors"))
			{
				BatchedMessage batch = new BatchedMessage();

				foreach(PlayerControl player in PlayerControl.AllPlayerControls)
				{
					if(Whitelist.ShouldSkip(player)) continue;
					batch.QueueSetColor(player, (byte)selectedColor);
				}

				batch.FinishBatch();
			}

			Bane.routines.discoHost.Enabled = Controls.GlobalPlayerSpecificToggle("Disco Party", Bane.routines.discoHost.targets);

			GUILayout.Label($"Color randomization delay: {Bane.routines.discoHost.RandomizationDelay:F2}s");
			Bane.routines.discoHost.RandomizationDelay = Controls.HorizontalSlider(Bane.routines.discoHost.RandomizationDelay, 0.1f, 2.0f);
		}

		private void ForceStartGame()
		{
			// Local lobbies are the only lobbies where we can start the game without host
			if(AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Start Game", "This feature can only be used if you are the host of the lobby.");
				return;
			}

			// The vanilla anticheat prevents players from sending a ClientReady message more than once
			// If we attempt to start the game twice, then all players will send another ClientReady message, and the entire lobby will be kicked
			if(AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started && Utilities.IsAnticheatPresent())
			{
				Bane.notifications.Send("Start Game", "The game has already been started.");
				return;
			}

			AmongUsClient.Instance.StartGame();

			// PlayerControl::RpcSetRole has checks against playing the intro cutscene in Freeplay
			// To avoid a blackscreen in Freeplay, we force the intro cutscene to start
			if(AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
			{
				HudManager.Instance.StartCoroutine(HudManager.Instance.CoShowIntro());
			}
		}

		private static void KillAllPlayers()
		{
			bool hasAnticheat = Utilities.IsAnticheatPresent();

			// Host may murder before start (lobby testing)
			if(hasAnticheat && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Murder Player", "This feature can only be used if you are the host of the lobby.");
				return;
			}

			// On +25 modded protocol lobbies, we are able to send SetColor RPCs as non-host
			// however we are still affected by message packing limits
			int packingLimit = AmongUsClient.Instance.GetMaxMessagePackingLimit();
			BatchedMessage batch = new BatchedMessage();

			foreach(PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if(Whitelist.ShouldSkip(player)) continue;

				if(batch.msgCount >= packingLimit)
				{
					batch.FinishBatch();
					batch = new BatchedMessage();
				}

				batch.QueueMurderPlayer(PlayerControl.LocalPlayer, player, MurderResultFlags.Succeeded);
			}

			batch.FinishBatch();
		}






		private static IEnumerator CloneMeAsDuplicate()
		{
			if (AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("Clone Me", "Need local player.", 3);
				yield break;
			}

			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Clone Me", "Host only on public AC.", 4);
				yield break;
			}

			NetworkedPlayerInfo srcInfo = PlayerControl.LocalPlayer.Data;
			if (srcInfo == null)
			{
				Bane.notifications.Send("Clone Me", "No local PlayerInfo.", 3);
				yield break;
			}

			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null)
			{
				Bane.notifications.Send("Clone Me", "No spawnable list.", 3);
				yield break;
			}

			InnerNetObject playerPrefab = null;
			for (int i = 0; i < list.Count; i++)
			{
				var obj = list[i];
				if (obj == null) continue;
				string n = obj.gameObject != null ? obj.gameObject.name : "";
				if (obj.SpawnId == 4 || n.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
				{
					playerPrefab = obj;
					break;
				}
			}

			if (playerPrefab == null)
			{
				Bane.notifications.Send("Clone Me", "Player prefab missing.", 3);
				yield break;
			}

			HashSet<byte> used = new HashSet<byte>();
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc != null)
					used.Add(pc.PlayerId);
			}
			byte newId = 0;
			for (byte id = 0; id < 255; id++)
			{
				if (!used.Contains(id))
				{
					newId = id;
					break;
				}
			}

			Vector2 pos = PlayerControl.LocalPlayer.transform.position;
			PlayerControl clonedPc = null;
			InnerNetObject bodyObj = null;
			bool infoOk = false;

			// --- PlayerInfo: clone LIVE Data object (prefab is empty → Serialize NRE) ---
			try
			{
				InnerNetObject infoObj = UnityEngine.Object.Instantiate(srcInfo).Cast<InnerNetObject>();
				NetworkedPlayerInfo clonedInfo = infoObj.TryCast<NetworkedPlayerInfo>();
				if (clonedInfo != null)
				{
					try { clonedInfo.PlayerId = newId; } catch { }
					try { clonedInfo.IsDead = false; } catch { }
					try { clonedInfo.Disconnected = false; } catch { }
					try { clonedInfo.OwnerId = -2; } catch { }

					BatchedMessage infoBatch = new BatchedMessage();
					infoBatch.QueueSpawn(infoObj, -2, SpawnFlags.None);
					infoBatch.FinishBatch();
					infoOk = true;
					Bane.Log.LogInfo("Clone Me: PlayerInfo spawn ok id=" + newId);
				}
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning("Clone Me PlayerInfo failed: " + e.Message);
				Bane.notifications.Send("Clone Me", "Info clone failed (body still spawning): " + e.Message, 5);
			}

			// --- Player body (works even if info fails) ---
			try
			{
				bodyObj = UnityEngine.Object.Instantiate(playerPrefab).Cast<InnerNetObject>();
				clonedPc = bodyObj.TryCast<PlayerControl>();
				bodyObj.transform.position = new Vector3(pos.x, pos.y, 0f);
				if (clonedPc != null)
				{
					try { clonedPc.PlayerId = newId; } catch { }
					try { clonedPc.NetTransform.SnapTo(pos); } catch { }
				}

				BatchedMessage bodyBatch = new BatchedMessage();
				bodyBatch.QueueSpawn(bodyObj, -2, SpawnFlags.None);
				bodyBatch.FinishBatch();
				FakePlayerTracker.Register(bodyObj, pos);
			}
			catch (Exception e)
			{
				Bane.Log.LogError("Clone Me body failed: " + e);
				Bane.notifications.Send("Clone Me", "Body failed: " + e.Message, 5);
				yield break;
			}

			for (int n = 0; n < 5; n++)
			{
				yield return new WaitForSecondsRealtime(0.1f);
				if (clonedPc == null) continue;
				try
				{
					BatchedMessage snap = new BatchedMessage();
					if (SpawnPlayerNetworkOnly)
						snap.QueueSnapToNetworkOnly(clonedPc, pos);
					else
					{
						clonedPc.transform.position = new Vector3(pos.x, pos.y, 0f);
						if (clonedPc.NetTransform != null)
							clonedPc.NetTransform.SnapTo(pos);
						snap.QueueSnapTo(clonedPc, pos);
					}
					snap.FinishBatch();
				}
				catch { }

				try
				{
					var outfit = srcInfo.DefaultOutfit;
					if (outfit != null)
						clonedPc.RpcSetColor((byte)outfit.ColorId);
				}
				catch { }
			}

			Bane.notifications.Send(
				"Clone Me",
				infoOk
					? "Clone body+info PlayerId=" + newId + " (body 20s TTL)."
					: "Clone body only PlayerId=" + newId + " (info Serialize still broken).",
				5);
		}

		private static IEnumerator SpawnPlayerAtMe()
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Spawn Player", "AmongUsClient missing.", 3);
				yield break;
			}

			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Spawn Player", "Host only when anticheat is present.", 4);
				yield break;
			}

			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null)
			{
				Bane.notifications.Send("Spawn Player", "No spawnable list.", 3);
				yield break;
			}

			InnerNetObject prefab = null;
			for (int i = 0; i < list.Count; i++)
			{
				var obj = list[i];
				if (obj == null) continue;
				string n = obj.gameObject != null ? obj.gameObject.name : "";
				if (obj.SpawnId == 4 || n.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
				{
					prefab = obj;
					break;
				}
			}

			if (prefab == null)
			{
				Bane.notifications.Send("Spawn Player", "Player prefab not found.", 3);
				yield break;
			}

			PlayerControl pc = null;
			InnerNetObject spawned = null;
			Vector2 pos = PlayerControl.LocalPlayer != null
				? (Vector2)PlayerControl.LocalPlayer.transform.position
				: Vector2.zero;

			try
			{
				spawned = UnityEngine.Object.Instantiate(prefab).Cast<InnerNetObject>();
				try { pc = spawned.TryCast<PlayerControl>(); } catch { }

				spawned.transform.position = new Vector3(pos.x, pos.y, 0f);
				if (!SpawnPlayerNetworkOnly && pc != null && pc.NetTransform != null)
				{
					try { pc.NetTransform.SnapTo(pos); } catch { }
				}

				BatchedMessage batch = new BatchedMessage();
				batch.QueueSpawn(spawned, -2, SpawnFlags.None);
				batch.FinishBatch();
				FakePlayerTracker.Register(spawned, pos);
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"Spawn Player failed: {e}");
				Bane.notifications.Send("Spawn Player", $"Failed: {e.Message}", 5);
				yield break;
			}

			for (int n = 0; n < 6; n++)
			{
				yield return new WaitForSecondsRealtime(0.08f);
				if (pc == null || pc.NetTransform == null) continue;
				try
				{
					BatchedMessage snap = new BatchedMessage();
					if (SpawnPlayerNetworkOnly)
					{
						snap.QueueSnapToNetworkOnly(pc, pos);
					}
					else
					{
						pc.transform.position = new Vector3(pos.x, pos.y, 0f);
						pc.NetTransform.SnapTo(pos);
						snap.QueueSnapTo(pc, pos);
					}
					snap.FinishBatch();
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning($"Spawn Player resnap {n}: {e.Message}");
				}
			}

			if (pc != null)
			{
				try { pc.RpcSetColor(2); } catch { }
			}

			Bane.Log.LogInfo($"Spawn Player: pinned at {pos} networkOnly={SpawnPlayerNetworkOnly}");
		}



		/// <summary>
		/// Ghost chat: spawn Player off-map, SetName, SendChat with TMP size/color, despawn.
		/// </summary>
		/// <summary>
		/// Ghost chat: off-map Player spawn → SendChat RPC only → despawn.
		/// Avoids QueueSendChat local AddChat (crashes on incomplete Player) and SetName.
		/// </summary>
		/// <summary>
		/// Ghost chat + optional AC probe via spawned Player NetId.
		/// Chat: network RPC from ghost (if NetId valid) + local AddChat so you always see something.
		/// </summary>

		private static IEnumerator SpawnPlayerAtNaN()
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Spawn NaN", "AmongUsClient missing.", 3);
				yield break;
			}
			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Spawn NaN", "Host only when anticheat is present.", 4);
				yield break;
			}

			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null)
			{
				Bane.notifications.Send("Spawn NaN", "No spawnable list.", 3);
				yield break;
			}

			InnerNetObject prefab = null;
			for (int i = 0; i < list.Count; i++)
			{
				var obj = list[i];
				if (obj == null) continue;
				string n = obj.gameObject != null ? obj.gameObject.name : "";
				if (obj.SpawnId == 4 || n.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
				{
					prefab = obj;
					break;
				}
			}
			if (prefab == null)
			{
				Bane.notifications.Send("Spawn NaN", "Player prefab not found.", 3);
				yield break;
			}

			InnerNetObject spawned = null;
			float nan = float.NaN;

			try
			{
				spawned = UnityEngine.Object.Instantiate(prefab).Cast<InnerNetObject>();
				// Position only - no SnapTo (that is self-only / AC bait on others)
				try { spawned.transform.position = new Vector3(nan, nan, 0f); } catch { }

				BatchedMessage batch = new BatchedMessage();
				batch.QueueSpawn(spawned, -2, SpawnFlags.None);
				batch.FinishBatch();
				try { FakePlayerTracker.Register(spawned, new Vector2(nan, nan)); } catch { }
			}
			catch (System.Exception e)
			{
				Bane.Log.LogError($"Spawn NaN failed: {e}");
				Bane.notifications.Send("Spawn NaN", $"Failed: {e.Message}", 5);
				yield break;
			}

			Bane.notifications.Send("Spawn NaN", "Spawned at NaN (no SnapTo)", 3);
			yield break;
		}


		private static IEnumerator SendGhostChat()
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Ghost Chat", "Not connected.", 3);
				yield break;
			}
			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Ghost Chat", "Host only when anticheat is present.", 4);
				yield break;
			}

			string rawMsg = string.IsNullOrEmpty(ghostChatMessage) ? " " : ghostChatMessage;
			int size = Mathf.Clamp(ghostChatSize, 10, 300);
			string col = (ghostChatColor ?? "FF0000").Trim().TrimStart('#');
			if (col.Length != 6) col = "FF0000";
			string rich = "<color=#" + col + "><size=" + size + ">" + rawMsg + "</size></color>";

			InnerNetObject spawned = null;
			PlayerControl pc = null;
			uint ghostNetId = 0;

			try
			{
				var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
				InnerNetObject prefab = null;
				if (list != null)
				{
					for (int i = 0; i < list.Count; i++)
					{
						var obj = list[i];
						if (obj == null) continue;
						string n = obj.gameObject != null ? obj.gameObject.name : "";
						if (obj.SpawnId == 4 || n.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
						{
							prefab = obj;
							break;
						}
					}
				}

				if (prefab != null)
				{
					Vector2 offMap = new Vector2(100000f, 100000f);
					spawned = UnityEngine.Object.Instantiate(prefab).Cast<InnerNetObject>();
					try { pc = spawned.TryCast<PlayerControl>(); } catch { pc = null; }
					try { spawned.transform.position = new Vector3(offMap.x, offMap.y, 0f); } catch { }

					BatchedMessage spawnBatch = new BatchedMessage();
					spawnBatch.QueueSpawn(spawned, -2, SpawnFlags.None);
					spawnBatch.FinishBatch();
				}
			}
			catch (System.Exception e)
			{
				Bane.Log.LogWarning("Ghost Chat spawn: " + e.Message);
			}

			// Wait for NetId assignment after spawn message
			for (int i = 0; i < 15; i++)
			{
				yield return new WaitForSecondsRealtime(0.05f);
				try
				{
					if (pc != null && pc.NetId != 0 && pc.NetId != uint.MaxValue)
					{
						ghostNetId = pc.NetId;
						break;
					}
					if (spawned != null && spawned.NetId != 0 && spawned.NetId != uint.MaxValue)
					{
						ghostNetId = spawned.NetId;
						break;
					}
				}
				catch { }
			}

			Bane.Log.LogInfo($"Ghost Chat: ghostNetId={ghostNetId} pc={(pc != null)}");

			// ONLY from ghost NetId - never as local player
			if (ghostNetId == 0)
			{
				Bane.notifications.Send("Ghost Chat", "No ghost NetId - spawn did not register. Nothing sent.", 5);
			}
			else
			{
				try
				{
					var w = AmongUsClient.Instance.StartRpcImmediately(
						ghostNetId, (byte)RpcCalls.SendChat, Hazel.SendOption.Reliable, -1);
					w.Write(rich);
					AmongUsClient.Instance.FinishRpcImmediately(w);
					Bane.Log.LogInfo("Ghost Chat: RPC only from ghost netId=" + ghostNetId);
					Bane.notifications.Send("Ghost Chat", $"Sent from ghost netId {ghostNetId} only", 4);
				}
				catch (System.Exception e)
				{
					Bane.notifications.Send("Ghost Chat", "Ghost RPC failed: " + e.Message, 5);
					Bane.Log.LogWarning("Ghost Chat: " + e);
				}
			}

			yield return new WaitForSecondsRealtime(0.3f);

			if (spawned != null)
			{
				try
				{
					BatchedMessage despawn = new BatchedMessage();
					despawn.QueueDespawn(spawned);
					despawn.FinishBatch();
				}
				catch
				{
					try { if (spawned.gameObject != null) UnityEngine.Object.Destroy(spawned.gameObject); } catch { }
				}
			}
		}

		/// <summary>
		/// Spawn off-map player, fire a few RPCs from ITS NetId, despawn.
		/// Hypothesis test: does AC attribute these to the ghost object / host instead of "you"?
		/// </summary>
		private static IEnumerator GhostAcProbe()
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Ghost AC Probe", "Not connected.", 3);
				yield break;
			}
			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Ghost AC Probe", "Host only when AC present.", 4);
				yield break;
			}

			InnerNetObject spawned = null;
			PlayerControl pc = null;
			uint ghostNetId = 0;

			try
			{
				var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
				InnerNetObject prefab = null;
				if (list != null)
				{
					for (int i = 0; i < list.Count; i++)
					{
						var obj = list[i];
						if (obj == null) continue;
						string n = obj.gameObject != null ? obj.gameObject.name : "";
						if (obj.SpawnId == 4 || n.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
						{
							prefab = obj;
							break;
						}
					}
				}
				if (prefab == null)
				{
					Bane.notifications.Send("Ghost AC Probe", "No Player prefab.", 3);
					yield break;
				}

				Vector2 offMap = new Vector2(100000f, 100000f);
				spawned = UnityEngine.Object.Instantiate(prefab).Cast<InnerNetObject>();
				try { pc = spawned.TryCast<PlayerControl>(); } catch { }
				try { spawned.transform.position = new Vector3(offMap.x, offMap.y, 0f); } catch { }

				BatchedMessage spawnBatch = new BatchedMessage();
				spawnBatch.QueueSpawn(spawned, -2, SpawnFlags.None);
				spawnBatch.FinishBatch();
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("Ghost AC Probe", "Spawn fail: " + e.Message, 4);
				yield break;
			}

			for (int i = 0; i < 20; i++)
			{
				yield return new WaitForSecondsRealtime(0.05f);
				try
				{
					if (pc != null && pc.NetId != 0 && pc.NetId != uint.MaxValue)
					{
						ghostNetId = pc.NetId;
						break;
					}
				}
				catch { }
			}

			if (ghostNetId == 0)
			{
				Bane.notifications.Send("Ghost AC Probe", "No NetId on ghost - abort.", 4);
			}
			else
			{
				Bane.Log.LogInfo("Ghost AC Probe: firing RPCs from netId=" + ghostNetId);
				int ok = 0;
				// SendChat
				try
				{
					var w = AmongUsClient.Instance.StartRpcImmediately(ghostNetId, (byte)RpcCalls.SendChat, Hazel.SendOption.Reliable, -1);
					w.Write("<size=20>ghost probe</size>");
					AmongUsClient.Instance.FinishRpcImmediately(w);
					ok++;
				}
				catch (System.Exception e) { Bane.Log.LogWarning("probe chat: " + e.Message); }
				// SetColor (often AC-sensitive when on real players)
				try
				{
					var w = AmongUsClient.Instance.StartRpcImmediately(ghostNetId, (byte)RpcCalls.SetColor, Hazel.SendOption.Reliable, -1);
					w.Write((byte)3);
					AmongUsClient.Instance.FinishRpcImmediately(w);
					ok++;
				}
				catch (System.Exception e) { Bane.Log.LogWarning("probe color: " + e.Message); }
				// SnapTo
				try
				{
					var w = AmongUsClient.Instance.StartRpcImmediately(ghostNetId, (byte)RpcCalls.SnapTo, Hazel.SendOption.Reliable, -1);
					w.Write(100000f);
					w.Write(100000f);
					w.WritePacked(0);
					AmongUsClient.Instance.FinishRpcImmediately(w);
					ok++;
				}
				catch (System.Exception e) { Bane.Log.LogWarning("probe snap: " + e.Message); }

				Bane.notifications.Send("Ghost AC Probe",
					$"netId={ghostNetId} sent ~{ok} RPCs - did you get kicked?", 6);
			}

			yield return new WaitForSecondsRealtime(0.5f);
			if (spawned != null)
			{
				try
				{
					var d = new BatchedMessage();
					d.QueueDespawn(spawned);
					d.FinishBatch();
				}
				catch { }
			}
		}

		private static void DespawnAllOfSelectedType(int index)
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Despawn", "Not in a lobby.", 3);
				return;
			}
			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Despawn", "Host only on public AC.", 4);
				return;
			}

			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null || index < 0 || index >= list.Count || list[index] == null)
			{
				Bane.notifications.Send("Despawn", "Invalid selection.", 3);
				return;
			}

			uint spawnId = list[index].SpawnId;
			string goName = list[index].gameObject != null ? list[index].gameObject.name : spawnId.ToString();
			int count = 0;

			List<InnerNetObject> found = new List<InnerNetObject>();
			try
			{
				foreach (InnerNetObject obj in UnityEngine.Object.FindObjectsOfType<InnerNetObject>())
				{
					if (obj == null) continue;
					if (obj.SpawnId != spawnId) continue;
					found.Add(obj);
				}
			}
			catch (Exception e)
			{
				Bane.Log.LogWarning($"FindObjectsOfType InnerNetObject failed: {e.Message}");
			}

			foreach (InnerNetObject obj in found)
			{
				try
				{
					obj.Despawn();
					count++;
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning($"Despawn {goName}: {e.Message}");
				}
			}

			Bane.notifications.Send("Despawn", $"Despawned {count}x {goName} (SpawnId {spawnId}).", 5);
		}

		private static void DespawnAllPlayers(bool keepLocal)
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Despawn Players", "Not in a lobby.", 3);
				return;
			}
			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Despawn Players", "Host only on public AC.", 4);
				return;
			}

			List<PlayerControl> snapshot = new List<PlayerControl>();
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc == null) continue;
				if (keepLocal && pc == PlayerControl.LocalPlayer) continue;
				if (Whitelist.ShouldSkip(pc)) continue;
				snapshot.Add(pc);
			}

			int count = 0;
			foreach (PlayerControl pc in snapshot)
			{
				try
				{
					string name = pc.Data != null ? pc.Data.PlayerName : "?";
					pc.Despawn();
					count++;
					Bane.Log.LogInfo($"Despawned player {name}");
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning($"Despawn player failed: {e.Message}");
					try
					{
						InnerNetObject net = pc.TryCast<InnerNetObject>();
						if (net != null)
						{
							net.Despawn();
							count++;
						}
					}
					catch { }
				}
			}

			Bane.notifications.Send(
				"Despawn Players",
				keepLocal
					? $"Despawned {count} players (you kept)."
					: $"Despawned {count} players (including attempts on self).",
				6);
		}

		private static void SpawnNonAddressable(int index, bool atMyPosition)
		{
			if (AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Spawn", "AmongUsClient missing.", 3);
				return;
			}

			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Spawn", "Host only when anticheat is present.", 4);
				return;
			}

			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null || index < 0 || index >= list.Count)
			{
				Bane.notifications.Send("Spawn", "Invalid spawn index.", 3);
				return;
			}

			InnerNetObject prefab = list[index];
			if (prefab == null)
			{
				Bane.notifications.Send("Spawn", "Prefab is null.", 3);
				return;
			}

			string goName = prefab.gameObject != null ? prefab.gameObject.name : prefab.GetType().Name;
			Bane.Log.LogInfo($"Spawning NonAddressable[{index}] {goName} SpawnId={prefab.SpawnId} atMe={atMyPosition}");

			try
			{
				InnerNetObject spawned = UnityEngine.Object.Instantiate(prefab).Cast<InnerNetObject>();

				Vector3 pos = Vector3.zero;
				if (atMyPosition && PlayerControl.LocalPlayer != null)
					pos = PlayerControl.LocalPlayer.transform.position;
				else if (!atMyPosition)
				{
					bool isPlayer = goName.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0
						|| prefab.SpawnId == 4;
					if (isPlayer && PlayerControl.LocalPlayer != null)
						pos = PlayerControl.LocalPlayer.transform.position;
				}

				if (pos != Vector3.zero || (atMyPosition && PlayerControl.LocalPlayer != null))
				{
					if (atMyPosition && PlayerControl.LocalPlayer != null)
						pos = PlayerControl.LocalPlayer.transform.position;
					spawned.transform.position = pos;
					try
					{
						var net = spawned.TryCast<PlayerControl>();
						if (net != null && net.NetTransform != null)
							net.NetTransform.SnapTo((Vector2)pos);
					}
					catch { }
				}

				BatchedMessage batch = new BatchedMessage();
				batch.QueueSpawn(spawned, -2, SpawnFlags.None);
				batch.FinishBatch();
				Bane.notifications.Send("Spawn", $"Spawned {goName}" + (atMyPosition ? " at you." : "."), 5);
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"SpawnNonAddressable failed: {e}");
				Bane.notifications.Send("Spawn", $"Failed: {e.Message}", 5);
			}
		}

		private static void SpawnLobby(bool atMyPosition)
		{
			Bane.Log.LogInfo($"Attempting to spawn in lobby atMe={atMyPosition}");

			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Spawn Lobby", "This feature can only be used if you are the host of the lobby.");
				return;
			}

			InnerNetObject lobbyPrefab = AmongUsClient.Instance.NonAddressableSpawnableObjects.First((obj) => obj.SpawnId == (uint)SpawnType.LobbyBehavior);
			if(lobbyPrefab == null)
			{
				Bane.Log.LogError($"Failed to find LobbyBehavior prefab in NonAddressableSpawnableObjects");
				return;
			}

			LobbyBehaviour lobby = UnityEngine.Object.Instantiate(lobbyPrefab).Cast<LobbyBehaviour>();
			if (atMyPosition && PlayerControl.LocalPlayer != null)
			{
				Vector3 pos = PlayerControl.LocalPlayer.transform.position;
				lobby.transform.position = pos;
			}

			BatchedMessage batch = new BatchedMessage();
			batch.QueueSpawn(lobby, -2, SpawnFlags.None);
			batch.FinishBatch();

			Bane.notifications.Send("Spawn Lobby", atMyPosition ? "Lobby spawned at you." : "A new instance of the lobby has been spawned.", 5);
		}

		private static void DespawnLobby()
		{
			Bane.Log.LogInfo($"Attempting to despawn lobby");

			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Despawn Lobby", "This feature can only be used if you are the host of the lobby.");
				return;
			}

			if(lobbyList.Count == 0)
			{
				Bane.notifications.Send("Despawn Lobby", "The lobby map has already been despawned.", 10);
				return;
			}

			InnerNetObject lobby = lobbyList.Dequeue();
			lobby.Despawn();

			Bane.notifications.Send("Despawn Lobby", "The lobby map has been despawned.", 5);
		}

		public static IEnumerator SpawnMap(byte mapId, bool atMyPosition = false)
		{
			Bane.Log.LogInfo($"Attempting to spawn in map id {mapId} atMe={atMyPosition}");

			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Spawn Map", "This feature can only be used if you are the host of the lobby.");
				yield break;
			}

			AsyncOperationHandle<GameObject> asyncHandle = AmongUsClient.Instance.ShipPrefabs[mapId].InstantiateAsync(null, false);
			yield return asyncHandle;

			ShipStatus ship = asyncHandle.Result.GetComponent<ShipStatus>();
			if (atMyPosition && PlayerControl.LocalPlayer != null)
			{
				Vector3 pos = PlayerControl.LocalPlayer.transform.position;
				ship.transform.position = pos;
			}

			BatchedMessage batch = new BatchedMessage();
			batch.QueueSpawn(ship, -2, SpawnFlags.None);
			batch.FinishBatch();

			Bane.notifications.Send("Spawn Map", $"{(MapNames)mapId} spawned" + (atMyPosition ? " at you." : "."), 5);
		}

		private static void DespawnMap()
		{
			Bane.Log.LogInfo($"Attempting to despawn map");

			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Despawn Map", "This feature can only be used if you are the host of the lobby.");
				return;
			}

			if(shipList.Count == 0)
			{
				Bane.notifications.Send("Despawn Map", "The current map has already been despawned.", 10);
				return;
			}

			InnerNetObject ship = shipList.Dequeue();
			ship.Despawn();

			Bane.notifications.Send("Despawn Map", "The current map has been despawned.", 5);
		}

		private void CloseMeeting()
		{
			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Close Meeting", "This feature can only be used if you are the host of the lobby.");
				return;
			}

			if(MeetingHud.Instance == null)
			{
				Bane.notifications.Send("Close Meeting", "This option can only be during a meeting.");
				return;
			}

			BatchedMessage batch = new BatchedMessage();
			batch.QueueVotingComplete(Array.Empty<MeetingHud.VoterState>(), null, false, false, 0);
			batch.QueueCloseMeeting();
			batch.FinishBatch();
		}

		private static IEnumerator ShapeshiftAll(PlayerControl target)
		{
			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Shapeshift Player", "You need to be the host of the lobby in order to use this feature.");
				yield break;
			}

			PlayerControl[] allPlayers = PlayerControl.AllPlayerControls.ToArray();
			foreach(PlayerControl player in allPlayers)
			{
				if(player == target || player.shapeshiftTargetPlayerId == target.PlayerId) continue;
				if(Whitelist.ShouldSkip(player)) continue;

				Utilities.ShapeshiftPlayer(player, target);

				// This function can send up to 42 reliable messages at once, so we need to implement a delay to avoid getting disconnected
				yield return Effects.Wait(0.05f);
			}
		}

		private static IEnumerator RevertAllShapeshift()
		{
			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Shapeshift Player", "You need to be the host of the lobby in order to use this feature.");
				yield break;
			}

			PlayerControl[] allPlayers = PlayerControl.AllPlayerControls.ToArray();
			foreach(PlayerControl player in allPlayers)
			{
				if(player.shapeshiftTargetPlayerId == -1) continue;
				if(Whitelist.ShouldSkip(player)) continue;

				Utilities.ShapeshiftPlayer(player, player);

				yield return Effects.Wait(0.05f);
			}
		}
	}
}