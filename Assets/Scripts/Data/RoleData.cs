using UnityEngine;

[CreateAssetMenu(fileName = "Role", menuName = "ScriptableObjects/Roles/Role")]
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

    // This allows to have more than once this role in a game
    [field: SerializeField]
    public bool CanHaveMultiples { get; private set; }

    // When CanHaveMultiples is false, there will be this exact number of this role at once in a game 
    [field: SerializeField]
    public int MandatoryCount { get; private set; }

    [field: SerializeField]
    public int[] GroupIndexes { get; private set; }

    [field: SerializeField]
    public int[] NightPriorities { get; private set; }

    [field: SerializeField]
    public int[] DayPriorities { get; private set; }
}
