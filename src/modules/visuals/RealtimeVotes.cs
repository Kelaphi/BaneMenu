using BaneMenu.network;
using InnerNet;
using System.Linq;
using UnityEngine;

namespace BaneMenu.modules.visuals
{
	internal class RealtimeVotes : Module
	{
		public RealtimeVotes() : base("RealtimeVotes")
		{
			/* default off until config */ base.Enabled = false;
		}

		private void OnNetObjectSpawn(InnerNetObject netObject)
		{
			if(netObject.SpawnId == (byte)SpawnType.MeetingHud)
			{
				MeetingHud.Instance.SkippedVoting.gameObject.SetActive(true);
			}
		}

		private void OnPlayerCastVote(NetworkedPlayerInfo voter, NetworkedPlayerInfo votee)
		{
			if(MeetingHud.Instance == null) return;
			if(MeetingHud.Instance.CurrentState == MeetingHud.MeetingStates.Results) return;

			if(votee != null)
			{
				PlayerVoteArea voteArea = MeetingHud.Instance.playerStates.FirstOrDefault((PlayerVoteArea voteArea) => voteArea.PlayerId == votee.PlayerId);
				if(voteArea == null) return;

				MeetingHud.Instance.BloopAVoteIcon(voter, 0, voteArea.transform);
			}
			else
			{
				MeetingHud.Instance.BloopAVoteIcon(voter, 0, MeetingHud.Instance.SkippedVoting.transform);
			}
		}

		private void OnVotingComplete(MeetingHud.VoterState[] voteStates, NetworkedPlayerInfo exiled, bool isTie, bool wasOverruled)
		{
			ClearBloopedVotes();
		}

		private void BloopAllVotes()
		{
			MeetingHud.Instance.SkippedVoting.gameObject.SetActive(true);

			foreach(PlayerVoteArea voteArea in MeetingHud.Instance.playerStates)
			{
				if(!voteArea.DidVote) continue;

				NetworkedPlayerInfo voter = GameData.Instance.GetPlayerById(voteArea.PlayerId);
				NetworkedPlayerInfo votee = GameData.Instance.GetPlayerById(voteArea.VotedForId);
				if(voter == null) continue;

				OnPlayerCastVote(voter, votee);
			}
		}

		private void ClearBloopedVotes()
		{
			if(MeetingHud.Instance == null) return;

			MeetingHud.Instance.SkippedVoting.gameObject.SetActive(false);

			foreach(PlayerVoteArea voteArea in MeetingHud.Instance.playerStates)
			{
				VoteSpreader spreader = voteArea.GetComponent<VoteSpreader>();
				ClearVoteSpreader(spreader);
			}

			VoteSpreader skipSpreader = MeetingHud.Instance.SkippedVoting.transform.GetComponent<VoteSpreader>();
			ClearVoteSpreader(skipSpreader);
		}

		private void ClearVoteSpreader(VoteSpreader spreader)
		{
			if(spreader == null) return;
			foreach(SpriteRenderer renderer in spreader.Votes)
			{
				renderer.enabled = false;
			}
			spreader.Votes.Clear();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EventCoordinator.OnNetObjectSpawn += OnNetObjectSpawn;
			EventCoordinator.OnPlayerCastVote += OnPlayerCastVote;
			EventCoordinator.OnVotingComplete += OnVotingComplete;

			if(MeetingHud.Instance != null && MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Results)
			{
				BloopAllVotes();
			}
		}

		protected override void OnDisable()
		{
			EventCoordinator.OnNetObjectSpawn -= OnNetObjectSpawn;
			EventCoordinator.OnPlayerCastVote -= OnPlayerCastVote;
			EventCoordinator.OnVotingComplete -= OnVotingComplete;

			if(MeetingHud.Instance != null && MeetingHud.Instance.CurrentState != MeetingHud.MeetingStates.Results)
			{
				ClearBloopedVotes();
			}
		}
	}
}
