using Assets.Scripts.Data.Tags;
using Assets.Scripts.Editor.Tags;
using UnityEngine;

public class GameplayData : ScriptableObject
{
    [field: SerializeField]
    [field: GameplayTagID]
    public GameplayTag GameplayTag { get; protected set; }

    [field: SerializeField]
    public string Name { get; protected set; }

    [field: SerializeField]
    [field: TextArea(8, 20)]
    public string Description { get; protected set; }

    [field: SerializeField]
    [field: PreviewSprite]
    public Sprite Image { get; protected set; }
}
