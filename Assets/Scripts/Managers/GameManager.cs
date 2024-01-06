using System;
using System.Collections.Generic;
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

    private RoleData[] _rolesToDistribute;

    public Dictionary<RoleBehavior, RoleData[]> ReservedRoles { get; private set; }

    private Dictionary<RoleBehavior, RoleData> _tempRoleBehaviors = new Dictionary<RoleBehavior, RoleData>();

    public event Action OnPreRoleDistribution = delegate { };
    public event Action OnPostRoleDistribution = delegate { };

    private readonly int MAX_ATTEMPT_COUNT = 100;
    private readonly Vector3 STARTING_DIRECTION = Vector3.back;

    protected override void Awake()
    {
        base.Awake();

        ReservedRoles = new Dictionary<RoleBehavior, RoleData[]>();
    }

    private void Start()
    {
        if (!_playerPrefab)
        {
            Debug.LogError("_playerPrefab of the GameManager is null");
            return;
        }

        SelectRolesToDistribute(_debugRolesSetupData.MandatoryRoles, new List<RoleSetup>(_debugRolesSetupData.AvailableRoles), _debugRolesSetupData.DefaultRole, _debugPlayerCount);
        OnPreRoleDistribution();
        LogRoles();
        CreatePlayers(_debugPlayerCount);
        OnPostRoleDistribution();
        // TODO: Start game loop
    }

    #region Gameplay Loop

    #region Roles selection
    private void SelectRolesToDistribute(RoleSetup[] mandatoryRoles, List<RoleSetup> availableRoles, RoleData defaultRole, int playerCount)
    {
        List<RoleData> rolesToDistribute = new List<RoleData>();

        // Add all mandatory roles first
        foreach (RoleSetup roleSetup in mandatoryRoles)
        {
            RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, ref rolesToDistribute);
            PrepareRoleBehaviors(addedRoles, ref rolesToDistribute, ref availableRoles);
        }

        List<RoleSetup> excludedRuleSetups = new List<RoleSetup>();
        int attempts = 0;

        // Complete with available roles at random or default role
        while (rolesToDistribute.Count < playerCount)
        {
            int startingRoleCount = rolesToDistribute.Count;

            if (availableRoles.Count <= 0 || attempts >= MAX_ATTEMPT_COUNT)
            {
                rolesToDistribute.Add(defaultRole);
                PrepareRoleBehavior(defaultRole, ref rolesToDistribute, ref availableRoles);

                continue;
            }

            int randomIndex = UnityEngine.Random.Range(0, availableRoles.Count);
            RoleSetup roleSetup = availableRoles[randomIndex];

            // Do not use roles setup that would add too many roles 
            if (excludedRuleSetups.Contains(roleSetup))
            {
                attempts++;
                continue;
            }
            else if (roleSetup.UseCount > playerCount - rolesToDistribute.Count)
            {
                excludedRuleSetups.Add(roleSetup);
                continue;
            }

            RoleData[] addedRoles = SelectRolesFromRoleSetup(roleSetup, ref rolesToDistribute);
            availableRoles.RemoveAt(randomIndex);

            PrepareRoleBehaviors(addedRoles, ref rolesToDistribute, ref availableRoles);

            // Some roles were removed from the list of roles to distribute
            if (startingRoleCount > rolesToDistribute.Count)
            {
                excludedRuleSetups.Clear();
                attempts = 0;
            }
        }

        _rolesToDistribute = rolesToDistribute.ToArray();
    }

    private RoleData[] SelectRolesFromRoleSetup(RoleSetup roleSetup, ref List<RoleData> rolesToDistribute)
    {
        List<RoleData> rolePool = new List<RoleData>(roleSetup.Pool);
        RoleData[] addedRoles = new RoleData[roleSetup.UseCount];

        for (int i = 0; i < roleSetup.UseCount; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, rolePool.Count);
            rolesToDistribute.Add(rolePool[randomIndex]);
            addedRoles[i] = rolePool[randomIndex];
            rolePool.RemoveAt(randomIndex);
        }

        return addedRoles;
    }
    
    private void PrepareRoleBehaviors(RoleData[] roles, ref List<RoleData> rolesToDistribute, ref List<RoleSetup> availableRoles)
    {
        foreach (RoleData role in roles)
        {
            PrepareRoleBehavior(role, ref rolesToDistribute, ref availableRoles);
        }
    }

    private void PrepareRoleBehavior(RoleData role, ref List<RoleData> rolesToDistribute, ref List<RoleSetup> availableRoles)
    {
        if (!role.Behavior)
        {
            return;
        }

        // Temporairy store the behaviors, because they must be attributed to specific players later
        RoleBehavior roleBehavior = Instantiate(role.Behavior, transform);
        _tempRoleBehaviors.Add(roleBehavior, role);

        roleBehavior.OnSelectedToDistribute(ref rolesToDistribute, ref availableRoles);
    }
    #endregion

    private void CreatePlayers(int playerCount)
    {
        Camera.main.transform.position = Camera.main.transform.position.normalized * _cameraOffset.Evaluate(playerCount);

        float rotationIncrement = 360.0f / playerCount;
        Vector3 startingPosition = STARTING_DIRECTION * _cardsOffset.Evaluate(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            Quaternion rotation = Quaternion.Euler(0, rotationIncrement * i, 0);
            Instantiate(_playerPrefab, rotation * startingPosition, rotation);
            // TODO: Give role (in seperate function?)
        }
    }
    #endregion

    public void ReserveRoles(RoleBehavior roleBehavior, RoleData[] roles)
    {
        ReservedRoles.Add(roleBehavior, roles);
    }

    private void LogRoles()
    {
        Debug.Log("-------------------------ROLES TO DISTRIBUTE-------------------------------------");

        foreach (RoleData roleData in _rolesToDistribute)
        {
            Debug.Log(roleData);
        }

        Debug.Log("---------------------------RESERVED ROLES-------------------------------------");

        foreach (KeyValuePair<RoleBehavior, RoleData[]> reservedRole in ReservedRoles)
        {
            string log = $"{reservedRole.Key.gameObject.name}:";

            foreach (RoleData role in reservedRole.Value)
            {
                log += "   [" + role.Name + "]";
            }

            Debug.Log(log);
        }
    }
}