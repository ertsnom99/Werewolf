using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Role", menuName = "ScriptableObjects/Role")]
public class RoleData : ScriptableObject
{
    [field: SerializeField]
    public string Name { get; private set; }

    [field: SerializeField]
    [field: TextArea(8, 20)]
    public string Description { get; private set; }

    [field: SerializeField]
    [field: TextArea(8, 20)]
    public string Instruction { get; private set; }

    [field: SerializeField]
    [field: PreviewSprite]
    public Sprite Image { get; private set; }

    [field: SerializeField]
    public int[] GroupIndexes { get; private set; }
}
