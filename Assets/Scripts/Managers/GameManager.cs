using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    [Header("Players")]
    [SerializeField]
    private GameObject _playerPrefab;

    [SerializeField]
    private AnimationCurve _cardsOffset;

    [SerializeField]
    private AnimationCurve _cameraOffset;

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField]
    private bool _useDebug;

    [SerializeField]
    private int _debugPlayerCount = 10;

    [SerializeField]
    private RolesSetupData _debugRolesSetupData;
#endif
    private readonly Vector3 STARTING_DIRECTION = Vector3.back;

    private void Start()
    {
        if (!_playerPrefab)
        {
            Debug.LogError("_playerPrefab of the GameManager is null");
            return;
        }

        // TODO: Pre role distribution check
        CreatePlayers(_debugPlayerCount);
        // TODO: Start game loop
    }

    private void CreatePlayers(int playerCount)
    {
        Camera.main.transform.position = Camera.main.transform.position.normalized * _cameraOffset.Evaluate(playerCount);

        float rotationIncrement = 360.0f / playerCount;
        Vector3 startingPosition = STARTING_DIRECTION * _cardsOffset.Evaluate(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            Quaternion rotation = Quaternion.Euler(0, rotationIncrement * i, 0);
            Instantiate(_playerPrefab, rotation * startingPosition, rotation);
            // TODO: Give role
        }
    }
}