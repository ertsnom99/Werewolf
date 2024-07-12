using UnityEngine;
using Werewolf.Data;

public class PlayerGroupsManager : WeakKeptMonoSingleton<PlayerGroupsManager>
{
	[field: SerializeField]
	public PlayerGroupsData PlayerGroupsData { get; private set; }

	protected override void Awake()
	{
		base.Awake();

		if (!PlayerGroupsData)
		{
			Debug.LogError($"The PlayerGroupsData of the PlayerGroupsManager is not set");
			return;
		}

		PlayerGroupsData.Init();
	}
}