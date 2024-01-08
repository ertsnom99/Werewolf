
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField]
    private GameObject _card;

    [SerializeField]
    private SpriteRenderer _roleImage;

    [Header("Role")]
    [SerializeField]
    [ReadOnly]
    private RoleData _role;

    [SerializeField]
    [ReadOnly]
    private RoleBehavior _behavior;
#if UNITY_EDITOR
    private void Awake()
    {
        if (!_card)
        {
            Debug.LogError($"_card of the player {gameObject.name} is null");
        }

        if (!_roleImage)
        {
            Debug.LogError($"_roleImage of the player {gameObject.name} is null");
        }
    }
#endif
    public void SetRole(RoleData role)
    {
        _role = role;
        _roleImage.sprite = role.Image;
    }

    public void SetBehavior(RoleBehavior behavior)
    {
        _behavior = behavior;
    }
}
