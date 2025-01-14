using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[SerializeField]
public enum CharacterState
{
    Actionable,
    Stopped,
    Idle,
    Move,
    Attack,
    Dodge
}


[System.Serializable]
public class Character : MonoBehaviour
{
    [SerializeField] public CharacterBaseState state;
    [SerializeField] public CharacterState currentState;
    [SerializeField] public List<Buff> activeBuffs;

    [SerializeField] public int baseHealth = 5;
    [SerializeField] public int currHealth = 5;
    [SerializeField] public int baseDefense = 0;
    [SerializeField] public float moveSpeed = 1f;

    [SerializeField] public int baseMeleeAffinity;
    [SerializeField] public int baseRangedAffinity;
    [SerializeField] public Rigidbody rgd;
    [SerializeField] public Vector2 axis;
    [SerializeField] public float turnSmoothVelocity;
    [SerializeField] public float turnSmoothTime;
    [SerializeField] public Transform followPoint; // Experimental transform to have minions follow this character
    [SerializeField] public float moveSpeedModifier = 1f; // Applies a modifier to moveSpeed, useful for terrain that slows

    [SerializeField] public Attack meleeAttack;
    [SerializeField] public Attack rangedAttack;


    [SerializeField] public Hitbox hitbox;
    [SerializeField] private AnimationOverrider overrider;
    [SerializeField] public Animator animator;

    //[SerializeField] public Image portrait;
    [SerializeField] public Sprite portrait;

    [SerializeField] public float dodgeDuration;
    [SerializeField] public float dodgeSpeedMultiplier;
    [SerializeField] public float dodgeInvulDuration;
    [SerializeField] public bool invulnerable = false;
    [SerializeField] CharacterStateTransitionRules stateRules;
    [SerializeField] float timeSinceLastHit;
    [SerializeField] float iFrameDuration;
    [SerializeField] float iFrameFlickerRate;
    float timeSinceLastFlicker;

    [SerializeField] public float interactRadius = 2f;
    [SerializeField] public string yarnKey;

    // Contains common functionality between entities (Leader, minions, enemies, NPCs)
    // Movement, attacking, dodging, health, stats etc

    void UpdateOverrider(Attack atk)
    {
        overrider.SetAnimations(atk.animController);
    }
    void UpdateOverrider()
    {
        overrider.SetAnimations(meleeAttack.animController);
    }
    void UpdateAnimatorParams()
    {
        if(rgd.velocity != Vector3.zero)
        {
            animator.SetFloat("Blend", 1);
        }
        else if(animator.GetFloat("Blend") > 0)
        {
            animator.SetFloat("Blend", animator.GetFloat("Blend") - 0.1f);
            
        }
        if(animator.GetFloat("Blend") < 0)
        {
            animator.SetFloat("Blend", 0);

        }
    }

    public void AttemptInteract()
    {
        // allow interaction only when the character can move
        if (!CanPerformStateTransition(CharacterState.Move))
            return;

        Collider[] colliders =
            Physics.OverlapSphere(transform.position, interactRadius);

        foreach(Collider col in colliders)
        {
            if(col.TryGetComponent<Character>(out Character ch))
            {
                if (ch.yarnKey != "")
                {
                    ch.DialogueStart(this);
                    return;
                }
            }
        }
    }

    // initiator = character who performed the interaction to start the dialogue
    public void DialogueStart(Character initiator)
    {
        // Have both characters look at each other
        // Tell dialogue runner to start a dialogue using this character's key
        transform.forward = (initiator.transform.position - transform.position).normalized;
        initiator.transform.forward = -transform.forward;

        DialogueRunnerEventTranslator.instance.StartDialogue(yarnKey);
    }

    public virtual void Hit(int damage)
    {
        if(damage < 0)
            damage = 0;
        currHealth -= damage;

        timeSinceLastHit = 0;

        if(currHealth <= 0)
        {
            // 
            EventManager.instance.CharacterDeath(this);
            Destroy(gameObject);
        }

        
    }

    public void Heal(int healValue)
    {
        currHealth += healValue;
        if(currHealth > GetMaxHealth())
        {
            currHealth = GetMaxHealth();
        }
    }

    public float GetMoveSpeed()
    {
        float moveSpeedBuffTotal = 0;
        foreach (Buff b in activeBuffs)
        {
            moveSpeedBuffTotal += (b.movementSpeedBuff * b.GetMultiplier());
        }

        if(PlayerCharacterManager.instance.activeRoom != null)
        {
            if(PlayerCharacterManager.instance.activeRoom.isRoomCleared)
            {
                // Causes bug where movement speeds of 0 will still allow slow movement
                // Problematic for tutorial dummies, disabling this mechanic until this gets fixed
                //moveSpeedBuffTotal += PlayerCharacterManager.instance.outOfCombatSpeedBoost;
            }
        }

        return moveSpeedBuffTotal + moveSpeed;
    }

    public int GetMaxHealth()
    {
        int maxHealthBuffTotal = 0;
        foreach(Buff b in activeBuffs)
        {
            maxHealthBuffTotal += (int)(b.maxHealthBuff * b.GetMultiplier());
        }
        return maxHealthBuffTotal + baseHealth;
    }

    public int GetDefense()
    {
        int defenseBuffTotal = 0;
        foreach (Buff b in activeBuffs)
        {
            defenseBuffTotal += (int)(b.defenseBuff * b.GetMultiplier());
        }
        return defenseBuffTotal;
    }

    public int GetMeleeAffinity()
    {
        int meleeAffinityBuffTotal = 0;
        foreach (Buff b in activeBuffs)
        {
            meleeAffinityBuffTotal += (int)(b.meleeAttackBuff * b.GetMultiplier());
        }
        return meleeAffinityBuffTotal + baseMeleeAffinity;
    }

    public int GetRangedAffinity()
    {
        int rangedAffinityBuffTotal = 0;
        foreach (Buff b in activeBuffs)
        {
            rangedAffinityBuffTotal += (int)(b.rangedAttackBuff * b.GetMultiplier());
        }
        return rangedAffinityBuffTotal + baseRangedAffinity;
    }



    public void Move(Vector2 ax, float modifier = 1)
    {
        axis = ax;

        if (!CanPerformStateTransition(CharacterState.Move))
        {
            return;
        }

        if (state.stateType != CharacterState.Move)
        {
            if (state.stateType == CharacterState.Attack)
                return;
            state = new MoveState(this);
        }

        moveSpeedModifier = modifier;
    }

    public void Move(Vector3 ax, float modifier = 1)
    {
        axis = new Vector2(ax.x, ax.z);

        Move(axis, modifier);
    }

    // Useful for when we want to continue keeping track of the char's axis but don't want them to move
    public void UpdateAxis(Vector2 ax)
    {
        axis = ax;

    }

    // Could set to idle state but that causes problems, consider scrapping idle state or adding a transition to 
    public void Stop()
    {
        //if (state.stateType != CharacterState.Idle && state.stateType != CharacterState.Attack)
        //    state = new IdleState(this);
        //else
            rgd.velocity = Vector3.zero;
    }

    public void SetMoveSpeedModifier(float newMod)
    {
        moveSpeedModifier = newMod;
    }

    public float GetMoveSpeedModifier()
    {
        return moveSpeedModifier;
    }

    public void SetFacingDirection(Vector3 newDirection)
    {
        transform.forward = newDirection;
        //transform.rotation
    }

    public void AttackStart()
    {
        // Play the animation
        if(state.stateType != CharacterState.Attack)
        {
            UpdateOverrider();
            animator.SetTrigger("Attack");
            state = new AttackState(this);
            EventManager.instance.AttackStarted(meleeAttack);
        }
    }

    public void AttackStart(Attack atk)
    {
        // Play the animation
        if (state.stateType != CharacterState.Attack)
        {
            UpdateOverrider(atk);
            animator.SetTrigger("Attack");
            state = new AttackState(this, atk);
            EventManager.instance.AttackStarted(atk);

        }
    }

    public bool GetInvulnerabilityState()
    {
        return timeSinceLastHit < iFrameDuration || invulnerable;
    }

    void InvulnerabilityFlicker()
    {
        timeSinceLastFlicker += Time.deltaTime;
        if (!GetInvulnerabilityState())
        {
            animator.gameObject.SetActive(true);

            return;
        }

        if(timeSinceLastFlicker >= iFrameFlickerRate)
        {
            animator.gameObject.SetActive(!animator.gameObject.activeInHierarchy);
            timeSinceLastFlicker = 0;
        }

    }

    // Start is called before the first frame update
    void Start()
    {
        state = new IdleState(this);
        timeSinceLastHit = 999;
    }

    // Update is called once per frame
    protected void Update()
    {
        currentState = state.stateType;
        state.Update();
        timeSinceLastHit += Time.deltaTime;
        InvulnerabilityFlicker();
    }

    // This is used for the sake of physics, if this becomes a problem later we can add FixedUpdate functions to State
    private void FixedUpdate()
    {
        UpdateAnimatorParams();

        state.FixedUpdate();
    }

    // Contains logic that decides if a state can be switched out of 
    // Basic for now 
    public static bool StateSwitch(Character c, CharacterState s)
    {
        if (c.state.canExit)
        {
            return true;
        }
        else return false;
    }

    // Should only be utilized in timelines, like in cutscenes
    IEnumerator ScriptedMovement(Vector3 newPos)
    {
        float originalDistance = Vector3.Distance(newPos, transform.position);
        while (Vector3.Distance(newPos, transform.position) > PlayerCharacterManager.instance.transitionStoppingDistance)
        {
            Vector3 dir = (newPos - transform.position).normalized;
            float elapsedDistance = Vector3.Distance(newPos, transform.position) / originalDistance;
            Move(new Vector2(dir.x, dir.z), elapsedDistance);
            Debug.Log(Vector3.Distance(newPos, transform.position));
            yield return null;
        }
    }

    public bool CanPerformStateTransition(CharacterState stateToAttempt)
    {
        if(stateRules.GetSelectedStates(state.stateType).Contains(stateToAttempt))
        {
            return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        //Gizmos.DrawLine(transform.position, transform.position + rgd.velocity);
        Gizmos.DrawLine(transform.position, transform.position + (transform.forward * .5f));
        if(GetInvulnerabilityState())
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 3f);
        }
    }
}

