using System;
using UnityEngine;

[Serializable]
public struct Group
{
    public int id;
    public string name;
    [HideInInspector]
    public GameObject leader;
}

[CreateAssetMenu(fileName = "Groups", menuName = "ScriptableObjects/Groups")]
public class GroupsData : ScriptableObject
{
    [field: SerializeField]
    public Group[] Groups { get; private set; }
}
