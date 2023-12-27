using UnityEngine;

[CreateAssetMenu(fileName = "Role", menuName = "ScriptableObjects/Role")]
public class RoleData : ScriptableObject
{
    [field: SerializeField]
    public string Name { get; protected set; }

    [field: SerializeField]
    [field: TextArea(8, 20)]
    public string Description { get; protected set; }

    [field: SerializeField]
    [field: TextArea(8, 20)]
    public string Instruction { get; protected set; }

    [field: SerializeField]
    [field: PreviewSprite]
    public Sprite Image { get; protected set; }
}
