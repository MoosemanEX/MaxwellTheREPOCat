//TODO: Finalise vision & networking

using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace MaxwellTheCat;

public class EnemyMaxwell : MonoBehaviour
{
    public enum State
    {
        Spawn = 0,
        Idle = 1,
        Roam = 2,
        LookAtPlayer = 3,
        AttackStart = 4,
        Explode = 5,
        Despawn = 6
    }

    private float tooCloseTimer = 0;
    
    public float tooCloseAllowance = 5;

    private PhotonView photonView;

    public State currentState;

    private bool stateImpulse;

    public float stateTimer;

    private float stateTicker;

    private Vector3 targetPosition;

    private float pitCheckTimer;

    private bool pitCheck;

    private Vector3 agentDestination;

    private bool visionPrevious;

    private Vector3 moveBackPosition;

    private float moveBackTimer;
    
    public Transform followOffsetTransform;

    [Space] public EnemyMaxwellAnim anim;

    public Enemy enemy;

    [SerializeField]private PlayerAvatar? playerTarget;

    [Space] private Quaternion rotationTarget;

    public SpringQuaternion rotationSpring;
    
    private ParticleScriptExplosion particleScriptExplosion;
    
    [Space] public Sound soundExplosion;

    public Sound soundExplosionGlobal;

    private bool hasTransformed = false;
    
    
    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        photonView = GetComponent<PhotonView>();
        tooCloseAllowance = Random.Range(5f, 20f);
        particleScriptExplosion = GetComponent<ParticleScriptExplosion>();
    }

    private void Update()
    {
        if ((GameManager.Multiplayer() && !PhotonNetwork.IsMasterClient) || !LevelGenerator.Instance.Generated) return;
        if (enemy.CurrentState == EnemyState.Despawn) UpdateState(State.Despawn);
        if (currentState != State.AttackStart && currentState != State.Explode && currentState != State.Despawn && currentState != State.Spawn)
        {
            TimerLogic(playerTarget);
        }
        
        GravityLogic();
        switch (currentState)
        {
            case State.Spawn:
                StateSpawn();
                break;
            case State.Idle:
                StateIdle();
                break;
            case State.Roam:
                StateRoam();
                break;
            case State.LookAtPlayer:
                StateLookAtPlayer();
                break;
            case State.AttackStart:
                StateAttackStart();
                break;
            case State.Explode:
                StateExplode();
                break;
            case State.Despawn:
                StateDespawn();
                break;
        }
    }

    private void StateSpawn()
    {
        if (stateImpulse)
        {
            enemy.NavMeshAgent.Warp(enemy.Rigidbody.transform.position);
            enemy.NavMeshAgent.ResetPath();
            enemy.Rigidbody.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
            playerTarget = null;
            hasTransformed = false;
            tooCloseAllowance = Random.Range(5f, 20f);
            tooCloseTimer = tooCloseAllowance;
            stateImpulse = false;
            stateTimer = 2f;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) UpdateState(State.Idle);
    }

    private void StateIdle()
    {
        TimerLogic(playerTarget);
        if (stateImpulse)
        {
            playerTarget = null;
            stateImpulse = false;
            stateTimer = Random.Range(2f, 5f);
            enemy.NavMeshAgent.Warp(enemy.Rigidbody.transform.position);
            enemy.NavMeshAgent.ResetPath();
        }
        if (!SemiFunc.EnemySpawnIdlePause())
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                UpdateState(State.Roam);
            }
        }
    }

    private void StateRoam()
    {
        if (stateImpulse)
        {
            stateImpulse = false;
            stateTimer = 5f;
            var flag = false;
            var levelPoint = SemiFunc.LevelPointGet(transform.position, 10f, 25f);
            if (!levelPoint) levelPoint = SemiFunc.LevelPointGet(transform.position, 0f, 999f);
            if ((bool)levelPoint &&
                NavMesh.SamplePosition(levelPoint.transform.position + Random.insideUnitSphere * 3f, out var hit, 5f,
                    -1) && Physics.Raycast(hit.position, Vector3.down, 5f, LayerMask.GetMask("Default")))
            {
                enemy.NavMeshAgent.SetDestination(hit.position);
                flag = true;
            }

            if (!flag) return;
            enemy.Rigidbody.notMovingTimer = 0f;
        }
        else
        {
            SemiFunc.EnemyCartJump(enemy);
            MoveBackPosition();
            if (enemy.Rigidbody.notMovingTimer > 2f) stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f || !enemy.NavMeshAgent.HasPath())
            {
                SemiFunc.EnemyCartJumpReset(enemy);
                UpdateState(State.Idle);
            }
        }
    }

    private void StateLookAtPlayer()
    {
        if (playerTarget != null && playerTarget.CompareTag("Player"))
        {
            enemy.transform.rotation = Quaternion.LookRotation(playerTarget.transform.position - enemy.Rigidbody.transform.position);
            enemy.NavMeshAgent.Warp(enemy.Rigidbody.transform.position);
            enemy.NavMeshAgent.ResetPath();
            enemy.Rigidbody.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition;

            Vector3 position = playerTarget.PlayerVisionTarget.VisionTransform.position;
            stateTimer -= Time.deltaTime;
            if (SemiFunc.PlayerVisionCheckPosition(enemy.Vision.VisionTransform.position, position, 7f, playerTarget, _previouslySeen: true) || SemiFunc.PlayerVisionCheckPosition(enemy.Vision.VisionTransform.position + base.transform.right * 0.25f, position + Vector3.down * 0.1f, 7f, playerTarget, _previouslySeen: true) || SemiFunc.PlayerVisionCheckPosition(enemy.Vision.VisionTransform.position - base.transform.right * 0.25f, position - Vector3.down * 0.1f, 7f, playerTarget, _previouslySeen: true) || SemiFunc.PlayerVisionCheckPosition(enemy.Vision.VisionTransform.position + base.transform.up * 0.25f, position + Vector3.down * 0.1f, 7f, playerTarget, _previouslySeen: true) || SemiFunc.PlayerVisionCheckPosition(enemy.Vision.VisionTransform.position - base.transform.up * 0.25f, position - Vector3.down * 0.1f, 7f, playerTarget, _previouslySeen: true))
            {
                stateTimer = 0.5f;
            }
            if (stateTimer <= 0f)
            {
                enemy.Rigidbody.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
                playerTarget = null;
                UpdateState(State.Idle);
            }
        }
    }
    
    private void StateAttackStart()
    {
        anim.ChangeToSkeleton();
        if (playerTarget != null && !hasTransformed)
        {
            enemy.transform.rotation =
                Quaternion.LookRotation(playerTarget.transform.position - enemy.Rigidbody.transform.position);
            enemy.Rigidbody.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition;
            CameraGlitch.Instance.PlayShort();
            hasTransformed = true;
        }
    }

    private void StateExplode()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            if (GameManager.Multiplayer())
            {
                photonView.RPC("PlayExplosionEffectsRPC", RpcTarget.All, transform.position);
            }
            else
            {
                PlayExplosionEffectsRPC(transform.position);
            }

            enemy.EnemyParent.Despawn();
        }
        
        UpdateState(State.Despawn);
    }

    private void StateDespawn()
    {
        if (stateImpulse)
        {
            enemy.NavMeshAgent.Warp(enemy.Rigidbody.transform.position);
            enemy.NavMeshAgent.ResetPath();
            stateImpulse = false;
        }
        
        UpdateState(State.Spawn);
    }

    public void OnHurt()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && currentState != State.AttackStart)
        {
            UpdateState(State.AttackStart);
        }
    }

    public void OnGrabbed()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && currentState != State.AttackStart)
        {
            enemy.Rigidbody.GrabRelease();
            playerTarget = enemy.Rigidbody.onGrabbedPlayerAvatar;
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("UpdatePlayerTargetRPC", RpcTarget.All, playerTarget.photonView.ViewID);
            }
            UpdateState(State.AttackStart);
        }
    }

    public void SetStateExplode()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            UpdateState(State.Explode);
        }
    }

    public void OnObjectHurt()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && (bool)enemy.Health.onObjectHurtPlayer &&
            currentState != State.AttackStart &&
            currentState !=
            State.Explode && currentState != State.Despawn)
        {
            playerTarget = enemy.Health.onObjectHurtPlayer;
            if (SemiFunc.IsMultiplayer())
                photonView.RPC("UpdatePlayerTargetRPC", RpcTarget.All, playerTarget.photonView.ViewID);
            UpdateState(State.AttackStart);
        }
    }

    private void UpdateState(State _state)
    {
        if (currentState != _state)
        {
            currentState = _state;
            stateImpulse = true;
            stateTimer = 0f;
            if (GameManager.Multiplayer())
            {
                photonView.RPC("UpdateStateRPC", RpcTarget.All, currentState);
            }
            else
            {
                UpdateStateRPC(currentState);
            }
        }
    }

    public void OnSpawn()
    {
        if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.EnemySpawn(enemy)) UpdateState(State.Spawn);
    }
    
    public void OnVisionTrigger()
    {
        if (enemy.CurrentState == EnemyState.Despawn || currentState == State.AttackStart || currentState == State.Explode || currentState == State.LookAtPlayer)
        {
            return;
        }
        PlayerAvatar? onVisionTriggeredPlayer = enemy.Vision.onVisionTriggeredPlayer;
        if (!SemiFunc.PlayerVisionCheckPosition(enemy.Vision.VisionTransform.position, onVisionTriggeredPlayer.PlayerVisionTarget.VisionTransform.position, enemy.Vision.VisionDistance, onVisionTriggeredPlayer, _previouslySeen: true))
        {
            playerTarget = null;
            return;
        }
        if (playerTarget != onVisionTriggeredPlayer && SemiFunc.IsMasterClientOrSingleplayer())
        {
            playerTarget = onVisionTriggeredPlayer;
            if (GameManager.Multiplayer())
            {
                photonView.RPC("UpdatePlayerTargetRPC", RpcTarget.All, playerTarget.photonView.ViewID);
            }
            UpdateState(State.LookAtPlayer);
        }
    }

    private void GravityLogic()
    {
        if (currentState == State.Explode)
            enemy.Rigidbody.gravity = false;
        else
            enemy.Rigidbody.gravity = true;
    }

    private void MoveBackPosition()
    {
        if (moveBackTimer <= 0f)
        {
            moveBackTimer = 0.1f;
            if (NavMesh.SamplePosition(transform.position, out var hit, 0.5f, -1) &&
                Physics.Raycast(transform.position, Vector3.down, 2f, LayerMask.GetMask("Default")))
                moveBackPosition = hit.position;
        }
    }

    private void TimerLogic(PlayerAvatar? closePlayer)
    {
        if (SemiFunc.IsMasterClientOrSingleplayer())
        {
            if (playerTarget != null)
            {
                tooCloseTimer += Time.deltaTime;
                if (closePlayer != null)
                {
                    playerTarget = closePlayer;
                }

                if (tooCloseTimer >= tooCloseAllowance)
                {
                    UpdateState(State.AttackStart);
                }
            }
            else if (playerTarget == null)
            {
                {
                    tooCloseTimer -= Time.deltaTime;
                    if (tooCloseTimer <= 0f)
                    {
                        tooCloseTimer = 0f;
                    }
                }
            }
            if (SemiFunc.IsMultiplayer() && playerTarget != null)
            {
                photonView.RPC("UpdatePlayerTargetRPC", RpcTarget.All, playerTarget.photonView.ViewID);
                //photonView.RPC("SyncAnimationRPC", RpcTarget.All);
            }
        }
    }
    

    [PunRPC]
    private void UpdateStateRPC(State _state)
    {
        currentState = _state;
        stateImpulse = true;
        if (currentState == State.AttackStart)
        {
            if (playerTarget != null)
            {
                if (playerTarget.isLocal){
                    playerTarget.physGrabber.ReleaseObject();
                CameraGlitch.Instance.PlayLong();
                }
            }
        }
        else if (currentState == State.Spawn && anim.isActiveAndEnabled)
        {
            anim.OnSpawn();
        }
    }

    [PunRPC]
    private void UpdatePlayerTargetRPC(int _photonViewID)
    {
        foreach (PlayerAvatar item in SemiFunc.PlayerGetList())
        {
            if (item.photonView.ViewID == _photonViewID)
            {
                playerTarget = item;
                break;
            }
        }
    }
    
    [PunRPC]
    private void PlayExplosionEffectsRPC(Vector3 position)
    {
        if (particleScriptExplosion == null)
            particleScriptExplosion = GetComponent<ParticleScriptExplosion>();
    
        particleScriptExplosion.Spawn(position, 5f, 50, 100, 5f, onlyParticleEffect: false, disableSound: true);
    
        soundExplosion.Play(position);
        soundExplosionGlobal.Play(position);
        
        GameDirector.instance.CameraShake.ShakeDistance(3f, 3f, 10f, position, 0.5f);
        GameDirector.instance.CameraImpact.ShakeDistance(3f, 3f, 10f, position, 0.05f);
    }

}