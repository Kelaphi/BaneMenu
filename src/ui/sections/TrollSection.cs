using InnerNet;
﻿using BepInEx.Unity.IL2CPP.Utils.Collections;
using Hazel;
using BaneMenu.assets;
using BaneMenu.modules;
using BaneMenu.network;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BaneMenu.ui.sections
{
	internal class TrollSection : Section
	{
		public TrollSection() : base("Troll") { }

		private int selectedVent = 0;
		private readonly System.Random rnd = new System.Random();

		public override void Render()
		{
			bool everyoneImmortal = BaneMenu.modules.self.Immortality.ImmortalAll;
			bool nextEveryone = Controls.Toggle(everyoneImmortal, "Disable Killing (everyone immortal)");
			if (nextEveryone != everyoneImmortal)
				BaneMenu.modules.self.Immortality.SetEveryone(nextEveryone);




			if(PlayerControl.LocalPlayer == null)
			{
				GUILayout.Label("You are not currently in a game, these options will not work.");
			}

			// ---- AuraMenu Nullstorm (fixed: Delay 0.07, RPC SetNamePlate) ----
			GUILayout.Label("Nullstorm");
			GUILayout.Label("Delay: 0.07  |  RPC: SetNamePlate");
			GUILayout.BeginHorizontal();
			if (Controls.Button("Nullstorm Attack"))
			{
				BaneMenu.modules.troll.Nullstorm.Activate();
			}
			if (BaneMenu.modules.troll.Nullstorm.RpcSpam && Controls.Button("Stop Nullstorm RPC Spam"))
			{
				BaneMenu.modules.troll.Nullstorm.Stop();
				Bane.notifications.Send("Nullstorm Attack", "RPC spam stopped.", 3);
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(4);

			// ---- AuraMenu Quick Rejoin ----
			// Delay = wait BEFORE CoJoinOnlineGameDirect (so prior RPCs e.g. votekicks settle).
			// Does not ExitGame first - matches Aura QueueRejoin + Rejoin(quick: true).
			GUILayout.Label("Quick Rejoin");
			GUILayout.Label($"Rejoin Delay: {BaneMenu.modules.misc.QuickRejoin.Delay:F2}s (wait, then direct join)");
			BaneMenu.modules.misc.QuickRejoin.Delay = Controls.HorizontalSlider(
				BaneMenu.modules.misc.QuickRejoin.Delay, 0.35f, 1f);
			if (Controls.Button("Quick Rejoin"))
			{
				BaneMenu.modules.misc.QuickRejoin.AuraQuickRejoinNow(BaneMenu.modules.misc.QuickRejoin.Delay);
				Bane.notifications.Send("Quick Rejoin", $"Direct join in {BaneMenu.modules.misc.QuickRejoin.Delay:F2}s…", 3);
			}
			GUILayout.Space(4);

			ModuleManager.antiStartGame.Enabled = Controls.Toggle(ModuleManager.antiStartGame.Enabled, "Anti Start Game");
						ModuleManager.blackout.Enabled = Controls.Toggle(ModuleManager.blackout.Enabled, "Blackout");
ModuleManager.crashLobby.Enabled = Controls.Toggle(ModuleManager.crashLobby.Enabled, "Queue Lobby Crash");
			ModuleManager.meetingSpam.Enabled = Controls.Toggle(ModuleManager.meetingSpam.Enabled, "Disable Meetings/Reports");
			ModuleManager.autoReportBodies.Enabled = Controls.PlayerSpecificToggle("Auto Report Bodies", PlayerControl.LocalPlayer, ref ModuleManager.autoReportBodies.target);
			Bane.routines.autoTriggerSpores.Enabled = Controls.Toggle(Bane.routines.autoTriggerSpores.Enabled, "Auto Trigger Spores");
			ModuleManager.disableCameras.Enabled = Controls.Toggle(ModuleManager.disableCameras.Enabled, "Disable Security Cameras");
			ModuleManager.disableCloseDoors.Enabled = Controls.Toggle(ModuleManager.disableCloseDoors.Enabled, "Disable Close Doors");
			ModuleManager.disableSabotages.Enabled = Controls.Toggle(ModuleManager.disableSabotages.Enabled, "Disable Sabotages");
			ModuleManager.disableVents.Enabled = Controls.Toggle(ModuleManager.disableVents.Enabled, "Disable Vents");
			Bane.routines.glitterBomb.Enabled = Controls.Toggle(Bane.routines.glitterBomb.Enabled, "Glitterbomb");
			Bane.routines.spamInfluencerCues.Enabled = Controls.Toggle(Bane.routines.spamInfluencerCues.Enabled, "Spam Influencer Cues");
			Bane.routines.kickOnTouch.LocalEnabled = Controls.Toggle(Bane.routines.kickOnTouch.LocalEnabled, "Kick on Touch");
			Bane.routines.kickOnTouch.KickerRole = Controls.Toggle(Bane.routines.kickOnTouch.KickerRole, "Kicker Role (client-side)");
			Bane.routines.orbit.Enabled = Controls.Toggle(Bane.routines.orbit.Enabled, "Circle Everyone");
			Bane.routines.bodySnatcher.Enabled = Controls.Toggle(Bane.routines.bodySnatcher.Enabled, "Vent Exile");

			// Suicide: murder yourself when allowed (impostor / host / no AC)
			bool canSuicide = false;
			if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null && !PlayerControl.LocalPlayer.Data.IsDead)
			{
				bool isImp = false;
				try { isImp = PlayerControl.LocalPlayer.Data.Role != null && PlayerControl.LocalPlayer.Data.Role.IsImpostor; } catch { }
				bool noAc = !Utilities.IsAnticheatPresent();
				bool host = AmongUsClient.Instance != null && Utilities.IsHostEffective();
				canSuicide = isImp || host || noAc;
			}
			if (canSuicide && Controls.Button("Suicide"))
			{
				PlayerControl me = PlayerControl.LocalPlayer;
				try
				{
					if (Utilities.IsHostEffective() || !Utilities.IsAnticheatPresent())
						me.RpcMurderPlayer(me, true);
					else
						me.CmdCheckMurder(me);
					Bane.notifications.Send("Suicide", "Sent self-murder.", 3);
				}
				catch (System.Exception e)
				{
					Bane.notifications.Send("Suicide", e.Message, 4);
				}
			}

			if(Controls.Button("Kick Host"))
			{
				BaneMenu.modules.troll.LobbyVoteKickLoop.KickHost();
			}
			// Host-auth only: spoof chat from every player (funny "consent")
			if (!Utilities.IsAnticheatPresent())
			{
				if (Controls.Button("Get Enchanting Consent"))
				{
					SendEnchantingConsent();
				}
			}


			// Force Host: VoteKick host → leave → rejoin (Hydralum method)
			GUILayout.BeginHorizontal();
			if (BaneMenu.modules.troll.ForceHost.IsRunning)
			{
				if (Controls.Button("Stop Force Host"))
					BaneMenu.modules.troll.ForceHost.Stop();
				int jc = BaneMenu.modules.troll.ForceHost.JoinCycle;
				string phase = jc <= 2 ? "kick all" : "host only";
				GUILayout.Label($"Force Host (join #{jc}, {phase}, host votes: {BaneMenu.modules.troll.ForceHost.HostsKicked})");
			}
			else
			{
				if (Controls.Button("Force Host"))
					BaneMenu.modules.troll.ForceHost.Start();
			}
			GUILayout.EndHorizontal();

			
			if (Utilities.IsHostEffective())
			{
				if(Controls.Button("Randomize Start Timer"))
				{
					if(PlayerControl.LocalPlayer == null)
					{
						Bane.notifications.Send("Start Timer", "Not in a lobby.", 3);
					}
					else
					{
						var batch = new BatchedMessage();
						batch.QueueSetStartCounter(PlayerControl.LocalPlayer, (sbyte)UnityEngine.Random.Range(-1, 120));
						batch.FinishBatch();
						Bane.notifications.Send("Start Timer", "Randomized start counter.", 3);
					}
				}
				if(Controls.Button("Randomize Lobby Expiration"))
				{
					if(LobbyBehaviour.Instance == null)
					{
						Bane.notifications.Send("Lobby Timer", "No lobby behaviour.", 3);
					}
					else
					{
						var batch = new BatchedMessage();
						batch.QueueLobbyTimeExpiring(UnityEngine.Random.Range(5, 600));
						batch.FinishBatch();
						Bane.notifications.Send("Lobby Timer", "Randomized lobby expiration.", 3);
					}
				}
			}

			if(Controls.Button("Kick All Players"))
			{
				// Enter+Boot are sent per-target inside KickPlayer (never global Enter - host AC detectable)
				foreach(PlayerControl player in PlayerControl.AllPlayerControls)
				{
					if(player == PlayerControl.LocalPlayer || player.OwnerId == AmongUsClient.Instance.HostId) continue;
					if(Whitelist.ShouldSkip(player)) continue;

					Utilities.KickPlayer(player);
				}
			}

			if(Controls.Button("Copy Random Player"))
			{
				PlayerControl randomPl = Utilities.GetRandomPlayer();
				Utilities.CopyPlayer(randomPl);
			}

			if(Controls.Button("Trigger All Spores"))
			{
				if(Utilities.GetCurrentMap() != MapNames.Fungle)
				{
					Bane.notifications.Send("Trigger Spores", "This option only works on the Fungle map.");
				}
				else
				{
					FungleShipStatus shipStatus = ShipStatus.Instance.Cast<FungleShipStatus>();

					BatchedMessage batch = new BatchedMessage();

					foreach(Mushroom mushroom in shipStatus.sporeMushrooms.Values)
					{
						batch.QueueTriggerSpore(PlayerControl.LocalPlayer, mushroom);
					}

					batch.FinishBatch();

					Bane.notifications.Send("Trigger Spores", "All spores have been triggered.", 5);
				}
			}

			if(Controls.Button("Deplete HnS Timer"))
			{
				AmongUsClient.Instance.StartCoroutine(DepleteSeekTimer().WrapToIl2Cpp());
			}

			SortedDictionary<int, string> vents = MapAssets.GetVents();

			GUILayout.Space(5);
			GUILayout.Label($"Vent Teleport:");
			Bane.routines.teleportSpammer.Enabled = Controls.GlobalPlayerSpecificToggle("Teleport Flooder", Bane.routines.teleportSpammer.targets);
			Bane.routines.ziplineSpammer.Enabled = Controls.GlobalPlayerSpecificToggle("Zipline Spammer", Bane.routines.ziplineSpammer.targets);

			GUILayout.Label($"Teleport everyone to vent: {vents.GetValueOrDefault(selectedVent, "N/A")}");
			selectedVent = Controls.HorizontalVentSlider(vents, selectedVent);

			if(Controls.Button("Teleport to Vent"))
			{
				foreach(PlayerControl player in PlayerControl.AllPlayerControls)
				{
					if(Whitelist.ShouldSkip(player)) continue;
					Teleporter.TeleportToVent(player, selectedVent);
				}
			}

			if(Controls.Button("Teleport to Random Vent"))
			{
				foreach(PlayerControl player in PlayerControl.AllPlayerControls)
				{
					if(player == PlayerControl.LocalPlayer || Whitelist.ShouldSkip(player)) continue;

					int ventId = rnd.Next(0, ShipStatus.Instance.AllVents.Count);

					Teleporter.TeleportToVent(player, ventId);
				}
			}

			GUILayout.Space(5);
			// Automatically close and open all doors at a set interval
			GUILayout.Label("Door Troller:");
			Bane.routines.doorTroller.Enabled = Controls.Toggle(Bane.routines.doorTroller.Enabled, "Enabled");

			GUILayout.Label($"Lock and Unlock Delay: {Bane.routines.doorTroller.LockAndUnlockDelay:F2}s");
			Bane.routines.doorTroller.LockAndUnlockDelay = Controls.HorizontalSlider(Bane.routines.doorTroller.LockAndUnlockDelay, 0.1f, 2.0f);

			GUILayout.Space(5);
			GUILayout.Label("Auto Expose Impostors:");
			ModuleManager.autoExposeImpostors.Enabled = Controls.Toggle(ModuleManager.autoExposeImpostors.Enabled, "Enabled");
			ModuleManager.autoExposeImpostors.ExposeOnMurder = Controls.Toggle(ModuleManager.autoExposeImpostors.ExposeOnMurder, "Expose On Murder");
			ModuleManager.autoExposeImpostors.ExposeOnShapeshift = Controls.Toggle(ModuleManager.autoExposeImpostors.ExposeOnShapeshift, "Expose On Shapeshift");
			ModuleManager.autoExposeImpostors.ExposeOnPhantom = Controls.Toggle(ModuleManager.autoExposeImpostors.ExposeOnPhantom, "Expose On Phantom");
		}

		// In Hide and Seek, completing a task will reduce the HnS hide timer depending on the length of the task (short, common, or long)
		// The problem is the game reduces the task timer even if we have already completed the task
		// so we can spam the CompleteTask RPC with the same task, and reduce the task timer to zero seconds
		private IEnumerator DepleteSeekTimer()
		{
			if(!GameManager.Instance.IsHideAndSeek())
			{
				Bane.notifications.Send("Deplete HnS Timer", "This feature can only be used in Hide and Seek.");
				yield break;
			}

			LogicGameFlowHnS gameFlow = GameManager.Instance.LogicFlow.Cast<LogicGameFlowHnS>();
			LogicOptionsHnS logicOptions = GameManager.Instance.LogicOptions.Cast<LogicOptionsHnS>();

			if(Utilities.IsHostEffective())
			{
				gameFlow.AdjustEscapeTimer(gameFlow.currentHideTime, true);
				yield break;
			}

			if(gameFlow.IsFinalCountdown)
			{
				Bane.notifications.Send("Deplete HnS Timer", "This feature cannot be used during the final hide time.");
				yield break;
			}

			NormalPlayerTask applicableTask = null;
			foreach(PlayerTask task in PlayerControl.LocalPlayer.myTasks)
			{
				NormalPlayerTask normalTask = task.TryCast<NormalPlayerTask>();
				if(normalTask == null) continue;

				Bane.Log.LogMessage($"Found applicable task {normalTask.TaskType}, task length of {normalTask.Length}");
				applicableTask = normalTask;

				if(normalTask.Length == NormalPlayerTask.TaskLength.Long) break;
			}

			if(applicableTask == null)
			{
				Bane.notifications.Send("Deplete HnS Timer", "This feature requires you to have at least one task.");
				yield break;
			}

			float completeDeduction;
			switch(applicableTask.Length)
			{
				case NormalPlayerTask.TaskLength.None:
				case NormalPlayerTask.TaskLength.Common:
				default:
					completeDeduction = logicOptions.GetCommonTaskTimeValue();
					break;

				case NormalPlayerTask.TaskLength.Short:
					completeDeduction = logicOptions.GetShortTaskTimeValue();
					break;

				case NormalPlayerTask.TaskLength.Long:
					completeDeduction = logicOptions.GetLongTaskTimeValue();
					break;
			}

			int requiredCompletions = (int)Math.Ceiling(gameFlow.currentHideTime / completeDeduction);

			Bane.Log.LogInfo($"Current escape time is {gameFlow.currentHideTime} and each task completion reduces the timer by {completeDeduction}s. We need to send the CompleteTask RPC {requiredCompletions} times to deplete the HnS timer.");

			BatchedMessage batch = new BatchedMessage();

			for(int i = 0; i <= requiredCompletions; i++)
			{
				// The message packing limit for non-hosts should be ten, but the Among Us anticheat disconnects us if we have more than six CompleteTask RPCs in a single batch
				if(batch.msgCount >= 6)
				{
					batch.FinishBatch();
					batch = new BatchedMessage();

					yield return Effects.Wait(0.05f);
				}

				batch.QueueCompleteTask(PlayerControl.LocalPlayer, applicableTask.Id);
			}

			batch.FinishBatch();
		}

		const string EnchantConsentMsg = "Sure! I do give my consent for you to enhance this lobby";

		static void SendEnchantingConsent()
		{
			if (AmongUsClient.Instance == null || PlayerControl.AllPlayerControls == null)
			{
				Bane.notifications.Send("Consent", "Not in a lobby.", 3);
				return;
			}
			if (Utilities.IsAnticheatPresent())
			{
				Bane.notifications.Send("Consent", "Host-auth lobbies only.", 4);
				return;
			}

			int n = 0;
			try
			{
				BatchedMessage batch = new BatchedMessage();
				foreach (var pl in PlayerControl.AllPlayerControls)
				{
					if (pl == null || pl.Data == null || pl.Data.Disconnected) continue;
					try
					{
						batch.QueueSendChat(pl, EnchantConsentMsg);
						n++;
						if (batch.msgCount >= 6)
						{
							batch.FinishBatch();
							batch = new BatchedMessage();
						}
					}
					catch (System.Exception e)
					{
						Bane.Log.LogWarning("Consent chat: " + e.Message);
					}
				}
				if (batch.msgCount > 0)
					batch.FinishBatch();
			}
			catch (System.Exception e)
			{
				Bane.notifications.Send("Consent", e.Message, 4);
				return;
			}

			Bane.notifications.Send("Consent", $"Enchanting consent x{n}", 4);
		}

	}
}
