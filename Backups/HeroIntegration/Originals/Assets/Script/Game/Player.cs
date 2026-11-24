using Input = GameInput;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.CrossPlatformInput;

public enum TraversalState { Idle, Walking, Sprinting, Crouching, Crawling, Airborne, Climbing, Hanging, PullingUp, Dead }
public struct TraversalInput
{
    public float Horizontal, Vertical;
    public bool Sprint, Crouch, Jump;
}

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Player : MonoBehaviour
{
    public event System.Action VoidEntered;
    public float Speed = 5f, MaxSpeed = 10f, JumpPower = 5f, jumpCooldown = .5f;
    public bool Grounded;
    public LayerMask collisionLayer;
    [Min(0)] public float acceleration, deceleration;
    [Range(0, 1)] public float airControl = 1f, minimumGroundNormalY = .5f;
    [Min(0)] public float jumpBufferTime = .1f;
    [Header("Hard traversal")]
    public StaminaSettings staminaSettings = new StaminaSettings();
    [Min(1)] public float sprintMultiplier = 2.5f;
    [Range(.2f, .6f)] public float crouchedBoxRatio = .45f;
    [Range(.1f, 1f)] public float crawlSpeedMultiplier = .45f;
    [Min(.1f)] public float climbSpeed = 1.5f, pullUpSpeed = 1.5f;
    [Min(.1f)] public float ledgeReach = .3f, maximumLedgeHeight = 1.05f;
    public TraversalState State { get; private set; }
    public TraversalStamina Stamina { get; private set; }
    public bool IsCrouched { get; private set; }
    public float SoleOffset { get; private set; }
    public float StandingTop { get; private set; }
    public float BaseGravityScale { get { return gravity; } }

    readonly ContactPoint2D[] contacts = new ContactPoint2D[32];
    readonly RaycastHit2D[] casts = new RaycastHit2D[16];
    readonly Collider2D[] overlaps = new Collider2D[16];
    Rigidbody2D rb2d;
    BoxCollider2D bodyBox;
    CircleCollider2D foot;
    SpriteRenderer spr;
    Animator anim;
    Vector2 standingSize, standingOffset;
    Vector3 initialScale;
    float gravity, simulationTime, lastJumpTime = float.NegativeInfinity, jumpRequestedUntil = float.NegativeInfinity;
    float horizontalInput, verticalInput, knockbackUntil;
    bool doubleJump, jumpQueued, sprintHeld, crouchHeld, touchSprint, touchCrouch;
    Vector2 ledgePoint, pullTarget;
    Collider2D gripCollider;
    Vector2 gripPosition;
    float gripFacing;
    bool regrabBlocked, pullRaised;
    TraversalEnvironment environment;
    SpriteRenderer crouchVisual;
    Vector2 previousPosition;
    bool enhancedAnimations, animationWasCrouched;
    int traversalAnimationLayer = -1;
    float crouchAnimationStarted;
    static readonly int TraversalPoseParameter = Animator.StringToHash("TraversalPose");
    static readonly int ClimbPlaybackParameter = Animator.StringToHash("ClimbPlayback");

    ContactFilter2D SolidFilter
    {
        get { var f = new ContactFilter2D { useTriggers = false }; f.SetLayerMask(collisionLayer); return f; }
    }
    PhysicsScene2D PhysicsScene { get { return gameObject.scene.GetPhysicsScene2D(); } }

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>(); bodyBox = GetComponent<BoxCollider2D>();
        foot = GetComponentInChildren<CircleCollider2D>();
        anim = GetComponent<Animator>(); spr = GetComponent<SpriteRenderer>();
        if (anim != null)
            foreach (AnimatorControllerParameter parameter in anim.parameters)
                if (parameter.nameHash == TraversalPoseParameter) enhancedAnimations = true;
        if (enhancedAnimations)
        { traversalAnimationLayer = anim.GetLayerIndex("Traversal Visuals"); if (traversalAnimationLayer >= 0) anim.SetLayerWeight(traversalAnimationLayer, 0); }
        standingSize = bodyBox.size; standingOffset = bodyBox.offset;
        initialScale = transform.localScale; gravity = rb2d.gravityScale;
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        SoleOffset = (standingOffset.y - standingSize.y * .5f) * scaleY;
        if (foot != null)
        {
            float footY = transform.InverseTransformPoint(foot.transform.TransformPoint(foot.offset)).y * scaleY;
            SoleOffset = Mathf.Min(SoleOffset, footY - foot.radius * Mathf.Abs(foot.transform.lossyScale.y));
        }
        StandingTop = (standingOffset.y + standingSize.y * .5f) * scaleY;
        Stamina = new TraversalStamina(staminaSettings);
        previousPosition = rb2d.position;
    }

    public void ConfigureTraversal(TraversalEnvironment value) { environment = value; }
    public void NotifyWarp()
    {
        previousPosition = rb2d.position;
        ReleaseGrip(); regrabBlocked = false;
        jumpQueued = false; jumpRequestedUntil = float.NegativeInfinity;
    }
    public void SetSprintHeld(bool held) { touchSprint = held; }
    public void SetCrouchHeld(bool held) { touchCrouch = held; }
    public void SampleTraversalInput(TraversalInput input)
    {
        horizontalInput = Mathf.Clamp(input.Horizontal, -1, 1);
        verticalInput = Mathf.Clamp(input.Vertical, -1, 1);
        sprintHeld = input.Sprint; crouchHeld = input.Crouch;
        if (input.Jump) { jumpQueued = true; jumpRequestedUntil = simulationTime + jumpBufferTime; }
    }

    void Update()
    {
        SampleTraversalInput(new TraversalInput {
            Horizontal = CrossPlatformInputManager.GetAxis("Horizontal"),
            Vertical = CrossPlatformInputManager.GetAxis("Vertical"),
            Jump = CrossPlatformInputManager.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W),
            Sprint = touchSprint || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            Crouch = touchCrouch || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)
        });
        if (anim != null)
        {
            anim.SetFloat("Speed", Mathf.Abs(rb2d.linearVelocity.x)); anim.SetBool("Grounded", Grounded);
            UpdateTraversalAnimation();
        }
    }

    // Presentation only: the existing physics state remains the authority.
    void UpdateTraversalAnimation()
    {
        if (!enhancedAnimations) return;
        if (IsCrouched && !animationWasCrouched) crouchAnimationStarted = Time.time;
        animationWasCrouched = IsCrouched;
        int pose = State == TraversalState.PullingUp ? 7 : State == TraversalState.Hanging ? 6 : State == TraversalState.Climbing ? 5 :
            IsCrouched ? (Time.time - crouchAnimationStarted < .18f ? 8 : Mathf.Abs(rb2d.linearVelocity.x) > .05f ? 4 : 3) :
            State == TraversalState.Airborne && rb2d.linearVelocity.y > .05f ? 2 : State == TraversalState.Sprinting ? 1 : 0;
        anim.SetInteger(TraversalPoseParameter, pose);
        if (traversalAnimationLayer >= 0) anim.SetLayerWeight(traversalAnimationLayer, pose == 0 ? 0 : 1);
        anim.SetFloat(ClimbPlaybackParameter, Mathf.Abs(rb2d.linearVelocity.y) / Mathf.Max(.01f, climbSpeed));
    }

    void FixedUpdate() { SimulateTraversal(Time.fixedDeltaTime); }

    public void SimulateTraversal(float dt)
    {
        if (State == TraversalState.Dead) return;
        simulationTime += dt;
        Vector2 position = rb2d.position;
        bool lethal = environment != null && environment.IsLethal(previousPosition, position);
        previousPosition = position;
        if (lethal)
        {
            State = TraversalState.Dead; rb2d.linearVelocity = Vector2.zero;
            VoidEntered?.Invoke(); return;
        }

        bool fresh = jumpQueued; jumpQueued = false;
        bool wantsJump = fresh || simulationTime <= jumpRequestedUntil;
        bool gripping = State == TraversalState.Hanging || State == TraversalState.PullingUp || State == TraversalState.Climbing;
        RefreshGroundContact(gripping);
        if (Grounded) { doubleJump = true; regrabBlocked = false; }
        if (regrabBlocked && Vector2.Distance(position, ledgePoint) > 1f) regrabBlocked = false;

        // Posture remains independent of locomotion: every expansion is clearance-checked.
        if (crouchHeld && !gripping) SetCrouched(true);
        else if (IsCrouched && CanStandAt(position, false)) SetCrouched(false);

        bool exerting = false;
        if (State == TraversalState.PullingUp)
        {
            exerting = true;
            if (!GripSurfaceExists() || !MovePullUp(dt)) ReleaseGrip();
        }
        else if (State == TraversalState.Hanging)
        {
            exerting = true;
            if (!HasValidGrip() || verticalInput < -.3f || crouchHeld || !Stamina.TrySpend(staminaSettings.LedgeHangDrainPerSecond * dt)) ReleaseGrip();
            else if (wantsJump || verticalInput > .3f)
            {
                jumpRequestedUntil = float.NegativeInfinity;
                if (CanStandAt(pullTarget, true) && Stamina.CanSpend(staminaSettings.LedgePullUpCost))
                {
                    Stamina.TrySpend(staminaSettings.LedgePullUpCost);
                    SetCrouched(false); pullRaised = false; State = TraversalState.PullingUp;
                }
            }
        }
        else if (State == TraversalState.Climbing)
        {
            exerting = true;
            bool dismount = Mathf.Abs(horizontalInput) > .2f && Mathf.Abs(verticalInput) < .2f;
            if (verticalInput <= 0) RefreshGroundContact(false);
            if (dismount || Grounded) ReleaseGrip();
            else if (Mathf.Abs(horizontalInput) > .2f && !regrabBlocked &&
                Stamina.CanSpend(staminaSettings.LedgeHangDrainPerSecond * dt)) TryGrabLedge();
            if (State == TraversalState.Hanging) Stamina.TrySpend(staminaSettings.LedgeHangDrainPerSecond * dt);
            else if (State != TraversalState.Climbing) { }
            else if (environment == null || !environment.CanClimb(position) || wantsJump ||
                !Stamina.TrySpend(staminaSettings.ClimbDrainPerSecond * dt)) ReleaseGrip();
            else rb2d.linearVelocity = new Vector2(0, verticalInput * climbSpeed);
        }
        else if (simulationTime >= knockbackUntil)
        {
            bool canClimb = environment != null && environment.CanClimb(position) && !IsCrouched &&
                Mathf.Abs(verticalInput) > .2f && (!Grounded || verticalInput > 0) && !wantsJump && Stamina.CanSpend(staminaSettings.ClimbDrainPerSecond * dt);
            if (canClimb)
            {
                State = TraversalState.Climbing; Grounded = false; rb2d.gravityScale = 0;
                rb2d.linearVelocity = new Vector2(0, verticalInput * climbSpeed);
                exerting = true; Stamina.TrySpend(staminaSettings.ClimbDrainPerSecond * dt);
            }
            else
            {
                rb2d.gravityScale = gravity;
                bool moving = Mathf.Abs(horizontalInput) > .01f;
                bool crawling = IsCrouched && moving;
                bool sprinting = !IsCrouched && sprintHeld && moving && Stamina.CanSpend(staminaSettings.SprintDrainPerSecond * dt);
                float multiplier = IsCrouched ? crawlSpeedMultiplier : (sprinting ? sprintMultiplier : 1f);
                if (crawling)
                {
                    if (Stamina.TrySpend(staminaSettings.CrawlDrainPerSecond * dt)) exerting = true;
                    else multiplier = 0; // Rest under the ceiling; recovery remains possible.
                }
                if (sprinting) { exerting = true; Stamina.TrySpend(staminaSettings.SprintDrainPerSecond * dt); }
                float target = Mathf.Clamp(horizontalInput * Speed * multiplier, -MaxSpeed, MaxSpeed);
                float rate = moving ? acceleration : deceleration;
                float control = Grounded ? 1 : airControl;
                float x = rate <= 0 ? Mathf.Lerp(rb2d.linearVelocity.x, target, control) :
                    Mathf.MoveTowards(rb2d.linearVelocity.x, target, rate * control * dt);
                rb2d.linearVelocity = new Vector2(x, rb2d.linearVelocity.y);
                State = !Grounded ? TraversalState.Airborne : IsCrouched ? (moving ? TraversalState.Crawling : TraversalState.Crouching) :
                    sprinting ? TraversalState.Sprinting : moving ? TraversalState.Walking : TraversalState.Idle;

                if (wantsJump && !IsCrouched && simulationTime - lastJumpTime >= jumpCooldown && (Grounded || doubleJump))
                {
                    bool fromGround = Grounded;
                    if (fromGround || Stamina.TrySpend(staminaSettings.DoubleJumpCost))
                    {
                        rb2d.AddForce(Vector2.up * JumpPower, ForceMode2D.Impulse);
                        lastJumpTime = simulationTime; jumpRequestedUntil = float.NegativeInfinity;
                        doubleJump = fromGround; Grounded = false; State = TraversalState.Airborne;
                        exerting |= !fromGround;
                    }
                }
                if (!Grounded && !IsCrouched && rb2d.linearVelocity.y <= .1f && moving && !regrabBlocked &&
                    Stamina.CanSpend(staminaSettings.LedgeHangDrainPerSecond * dt)) TryGrabLedge();
                // Never flip compound colliders while gripping a ledge.
                if (State != TraversalState.Hanging && moving)
                    transform.localScale = new Vector3(Mathf.Abs(initialScale.x) * Mathf.Sign(horizontalInput), initialScale.y, initialScale.z);
            }
        }
        Stamina.Recover(dt, Grounded, Mathf.Abs(rb2d.linearVelocity.x) > .05f, exerting);
    }

    void RefreshGroundContact(bool gripping)
    {
        Grounded = false;
        if (gripping || rb2d.linearVelocity.y > .1f) return;
        int count = rb2d.GetContacts(SolidFilter, contacts);
        for (int i = 0; i < count; i++)
            if (contacts[i].normal.y >= minimumGroundNormalY) { Grounded = true; return; }
    }

    void SetCrouched(bool value)
    {
        if (value == IsCrouched) return;
        IsCrouched = value;
        bodyBox.size = value ? new Vector2(standingSize.x, standingSize.y * crouchedBoxRatio) : standingSize;
        bodyBox.offset = value ? new Vector2(standingOffset.x, standingOffset.y - (standingSize.y - bodyBox.size.y) * .5f) : standingOffset;
    }

    public bool CanStandAt(Vector2 position, bool includeFoot)
    {
        Vector2 scale = new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        Vector2 center = position + (Vector2)transform.TransformVector(standingOffset);
        int count = PhysicsScene.OverlapBox(center, Vector2.Scale(standingSize, scale) - Vector2.one * .01f, transform.eulerAngles.z, SolidFilter, overlaps);
        for (int i = 0; i < count; i++) if (overlaps[i].attachedRigidbody != rb2d) return false;
        if (includeFoot && foot != null)
        {
            Vector2 offset = (Vector2)foot.transform.TransformPoint(foot.offset) - (Vector2)transform.position;
            count = PhysicsScene.OverlapCircle(position + offset, foot.radius * Mathf.Max(Mathf.Abs(foot.transform.lossyScale.x), Mathf.Abs(foot.transform.lossyScale.y)) - .005f, SolidFilter, overlaps);
            for (int i = 0; i < count; i++) if (overlaps[i].attachedRigidbody != rb2d) return false;
        }
        return true;
    }

    void TryGrabLedge()
    {
        float facing = Mathf.Sign(horizontalInput);
        float reach = bodyBox.bounds.extents.x + ledgeReach;
        Vector2 chest = rb2d.position + Vector2.up * (StandingTop - .12f);
        int hits = PhysicsScene.Raycast(chest, Vector2.right * facing, reach, SolidFilter, casts);
        var wall = hits > 0 ? casts[0] : default(RaycastHit2D);
        if (wall.collider == null || wall.distance <= 0 || Mathf.Abs(wall.normal.x) < .8f) return;
        Vector2 above = new Vector2(wall.point.x + facing * .08f, rb2d.position.y + StandingTop + .35f);
        hits = PhysicsScene.Raycast(above, Vector2.down, .65f, SolidFilter, casts);
        var top = hits > 0 ? casts[0] : default(RaycastHit2D);
        if (top.collider != wall.collider || top.distance <= 0 || top.normal.y < .9f || top.point.y - (rb2d.position.y + SoleOffset) > maximumLedgeHeight) return;
        Vector2 target = new Vector2(top.point.x + facing * (bodyBox.bounds.extents.x + .06f), top.point.y - SoleOffset + .025f);
        if (!CanStandAt(target, true)) return;
        ledgePoint = new Vector2(wall.point.x, top.point.y); pullTarget = target;
        gripCollider = wall.collider; gripPosition = rb2d.position; gripFacing = facing;
        State = TraversalState.Hanging; Grounded = false; rb2d.gravityScale = 0; rb2d.linearVelocity = Vector2.zero;
    }

    bool GripSurfaceExists()
    {
        if (gripCollider == null || !gripCollider.enabled || !gripCollider.gameObject.activeInHierarchy) return false;
        Vector2 chest = gripPosition + Vector2.up * (StandingTop - .12f);
        int hits = PhysicsScene.Raycast(chest, Vector2.right * gripFacing, bodyBox.bounds.extents.x + ledgeReach, SolidFilter, casts);
        if (hits == 0 || casts[0].collider != gripCollider || casts[0].distance <= 0 ||
            Mathf.Abs(casts[0].normal.x) < .8f || Mathf.Abs(casts[0].point.x - ledgePoint.x) > .025f) return false;
        Vector2 above = new Vector2(casts[0].point.x + gripFacing * .08f, gripPosition.y + StandingTop + .35f);
        hits = PhysicsScene.Raycast(above, Vector2.down, .65f, SolidFilter, casts);
        return hits > 0 && casts[0].collider == gripCollider && casts[0].distance > 0 &&
            casts[0].normal.y >= .9f && Mathf.Abs(casts[0].point.y - ledgePoint.y) <= .025f;
    }

    bool HasValidGrip()
    {
        // Keep the captured anchor; never teleport back to an obsolete edge or switch sides.
        return (rb2d.position - gripPosition).sqrMagnitude <= .025f * .025f && GripSurfaceExists();
    }

    bool MovePullUp(float dt)
    {
        Vector2 waypoint = pullRaised ? pullTarget : new Vector2(rb2d.position.x, pullTarget.y);
        Vector2 delta = waypoint - rb2d.position;
        if (delta.magnitude < .015f)
        {
            if (!pullRaised) { pullRaised = true; return true; }
            ReleaseGrip(); return true;
        }
        float distance = Mathf.Min(delta.magnitude, pullUpSpeed * dt);
        int count = rb2d.Cast(delta.normalized, SolidFilter, casts, distance + .005f);
        for (int i = 0; i < count; i++)
            if (casts[i].collider != null && Vector2.Dot(casts[i].normal, delta.normalized) < -.05f && casts[i].distance < distance) return false;
        rb2d.MovePosition(rb2d.position + delta.normalized * distance);
        return true;
    }

    void ReleaseGrip()
    {
        gripCollider = null;
        regrabBlocked = true; State = TraversalState.Airborne; Grounded = false;
        rb2d.gravityScale = gravity; doubleJump = false;
    }

    void LateUpdate()
    {
        if (spr == null) return;
        if (!IsCrouched)
        {
            if (crouchVisual != null) { crouchVisual.enabled = false; spr.enabled = true; }
            return;
        }
        if (crouchVisual == null)
        {
            var visual = new GameObject("Crouched visual", typeof(SpriteRenderer));
            visual.transform.SetParent(transform, false);
            crouchVisual = visual.GetComponent<SpriteRenderer>(); crouchVisual.sharedMaterial = spr.sharedMaterial;
        }
        crouchVisual.sprite = spr.sprite; crouchVisual.color = spr.color;
        crouchVisual.sortingLayerID = spr.sortingLayerID; crouchVisual.sortingOrder = spr.sortingOrder;
        crouchVisual.flipX = spr.flipX; crouchVisual.flipY = spr.flipY;
        if (enhancedAnimations)
        {
            // The artwork already depicts the crouched pose; do not shrink it again.
            // Anchor its soles without changing the body or its colliders.
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            crouchVisual.transform.localScale = Vector3.one;
            crouchVisual.transform.localPosition = new Vector3(0, SoleOffset / scaleY - (spr.sprite != null ? spr.sprite.bounds.min.y : 0), 0);
            crouchVisual.enabled = true; spr.enabled = false; return;
        }
        float height = standingOffset.y - standingSize.y * .5f + standingSize.y * crouchedBoxRatio - SoleOffset;
        float ratio = spr.sprite != null ? height / Mathf.Max(.01f, spr.sprite.bounds.size.y) : .4f;
        crouchVisual.transform.localScale = new Vector3(1, ratio, 1);
        crouchVisual.transform.localPosition = new Vector3(0, spr.sprite != null ? spr.sprite.bounds.min.y * (1 - ratio) : 0, 0);
        crouchVisual.enabled = true; spr.enabled = false;
    }

    // Visibility is a rendering concern. Lethal regions, not the camera, decide death.
    public void EnemyJump() { }
    public void EnemyKnocBack(float enemyPosX)
    {
        ReleaseGrip(); rb2d.AddForce(Vector2.left * Mathf.Sign(enemyPosX - transform.position.x) * JumpPower, ForceMode2D.Impulse);
        knockbackUntil = simulationTime + 1.5f;
    }
    public void Destruir() { Destroy(gameObject, 1f); }
}
