using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    public List<RoleData> RolesToDistribute { get; private set; }

    public Dictionary<RoleBehavior, RoleData[]> ReservedRoles { get; private set; }

    private Dictionary<RoleBehavior, RoleData> _unassignedRoleBehaviors = new Dictionary<RoleBehavior, RoleData>();

    private Player[] _players;

    [Header("Layout")]
    [SerializeField]
    private Player _playerPrefab;

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

    public event Action OnPreRoleDistribution = delegate { };
    public event Action OnPostRoleDistribution = delegate { };

    private readonly int MAX_ATTEMPT_COUNT = 100;
    private readonly Vector3 STARTING_DIRECTION = Vector3.back;

    protected override void Awake()
    {
        base.Awake();

        RolesToDistribute = new List<RoleData>();
        ReservedRoles = new Dictionary<RoleBehavior, RoleData[]>();
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (!_playerPrefab)
        {
            Debug.LogError("_playerPrefab of the GameManager is null");
            return;
        }
#endif
        SelectRolesToDistribute(_debugRolesSetupData.MandatoryRoles, new List<RoleSetup>(_debugRolesSetupData.AvailableRoles), _debugRolesSetupData.DefaultRole, _debugPlayerCount);
        CreatePlayers(_debugPlayerCount);
        AdjustCamera(_debugPlayerCount);
        OnPreRoleDistribution();
        DistributeRoles();
        OnPostRoleDistribution();
        // TODO: Display reserved roles
        LogRoles();
        // TODO: Start game loop
    }

    private void LogRoles()
    {
        Debug.Log("------------------------ROLES TO DISTRIBUTE-------------------------------------");

        foreach (RoleData role in RolesToDistribute)
        {
            Debug.Log(role);
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

    #region Pre Gameplay Loop

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

        RolesToDistribute = rolesToDistribute;
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
    
    public void PrepareRoleBehaviors(RoleData[] roles, ref List<RoleData> rolesToDistribute, ref List<RoleSetup> availableRoles)
    {
        foreach (RoleData role in roles)
        {
            PrepareRoleBehavior(role, ref rolesToDistribute, ref availableRoles);
        }
    }

    public void PrepareRoleBehavior(RoleData role, ref List<RoleData> rolesToDistribute, ref List<RoleSetup> availableRoles)
    {
        if (!role.Behavior)
        {
            return;
        }

        // Temporairy store the behaviors, because they must be attributed to specific players later
        RoleBehavior roleBehavior = Instantiate(role.Behavior, transform);
        _unassignedRoleBehaviors.Add(roleBehavior, role);

        roleBehavior.Init();
        roleBehavior.OnSelectedToDistribute(ref rolesToDistribute, ref availableRoles);
    }
    #endregion

    private void CreatePlayers(int playerCount)
    {
        _players = new Player[playerCount];

        float rotationIncrement = 360.0f / playerCount;
        Vector3 startingPosition = STARTING_DIRECTION * _cardsOffset.Evaluate(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            Quaternion rotation = Quaternion.Euler(0, rotationIncrement * i, 0);
            _players[i] = Instantiate(_playerPrefab, rotation * startingPosition, rotation);
        }
    }

    private void AdjustCamera(int playerCount)
    {
        Camera.main.transform.position = Camera.main.transform.position.normalized * _cameraOffset.Evaluate(playerCount);
    }

    #region Roles distribution
    private void DistributeRoles()
    {
        foreach (Player player in _players)
        {
            RoleData selectedRole = RolesToDistribute[UnityEngine.Random.Range(0, RolesToDistribute.Count)];
            player.SetRole(selectedRole);
            RolesToDistribute.Remove(selectedRole);

            foreach (KeyValuePair<RoleBehavior, RoleData> roleBehavior in _unassignedRoleBehaviors)
            {
                if (roleBehavior.Value == selectedRole)
                {
                    roleBehavior.Key.transform.parent = player.transform;
                    roleBehavior.Key.transform.localPosition = Vector3.zero;

                    player.SetBehavior(roleBehavior.Key);
                    _unassignedRoleBehaviors.Remove(roleBehavior.Key);
                    break;
                }
            }
        }
    }

    public void AddRolesToDistribute(RoleData[] roles)
    {
        RolesToDistribute.AddRange(roles);
    }

    public void RemoveRoleToDistribute(RoleData role)
    {
        RolesToDistribute.Remove(role);
    }

    public void ReserveRoles(RoleBehavior roleBehavior, RoleData[] roles)
    {
        ReservedRoles.Add(roleBehavior, roles);
    }
    #endregion

    #endregion
}