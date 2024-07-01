using UnityEngine;
using Werewolf.Data;

public class PlayerGroupsManager : WeakKeptMonoSingleton<PlayerGroupsManager>
{
	[field: SerializeField]
	public PlayerGroupsData PlayerGroupsData { get; private set; }
}