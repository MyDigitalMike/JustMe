#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary, request-driven measurements. No fixture is added to a saved scene.
[InitializeOnLoad]
public static class Phase3MovementChecks
{
    const float Dt = .02f;
    static readonly string Request = Path.Combine("Temp", "JustMePhase3", "movement.request");
    static readonly string Output = Path.Combine("Backups", "Phase3", "movement-measurements.json");
    static bool running;

    [Serializable]
    public sealed class Check
    {
        public string name;
        public bool passed;
        public string detail;
    }

    [Serializable]
    public sealed class TrajectorySample
    {
        public float time, x, y;
    }

    [Serializable]
    public sealed class JumpMeasurement
    {
        public bool landed, secondJumpAccepted;
        public float peakRise, flightSeconds, horizontalReach, staminaSpent, secondJumpStaminaCost;
        public float runUpSeconds = .4f, secondPressSeconds = .24f, horizontalInput = 1;
        public bool sprintHeld;
        public List<TrajectorySample> samples = new List<TrajectorySample>();
    }

    [Serializable]
    public sealed class LedgeMeasurement
    {
        public bool grabbed, enteredPullUp, verticalThenHorizontal, landed;
        public float hangX, hangY, pullUpSeconds, pullUpStaminaCost, landingX, landingY;
    }

    [Serializable]
    public sealed class ClimbExitMeasurement
    {
        public bool mirrored, enteredClimb, grabbed, enteredPullUp, stoodOnPlatform, stayedStanding, noSolidOverlap, exhaustedBeforeStanding;
        public bool standingProbeAlwaysClear, standingProbeAtLanding;
        public float minimumNativeSeparation, minimumNativeSeparationTime, firstInvalidPenetrationTime, firstInvalidPenetrationDistance;
        public float baselineFootSeparation, baselineCompoundSeparation, configuredContactOffset, excessPenetrationBeyondRestBaseline, landingFootSeparation;
        public float baselineFeetHeight;
        public bool baselineStandingBoxProbeClear, baselineStandingFootProbeClear, landingStandingBoxProbeClear;
        public string baselineCompoundPair;
        public string minimumNativeSeparationPair, minimumNativeSeparationState, firstInvalidPenetrationPair, firstInvalidPenetrationState;
        public float firstStandingProbeFailureTime, firstStandingProbeFailureX, firstStandingProbeFailureY;
        public string firstStandingProbeFailureState, firstStandingProbeFailureBlocker;
        public float startingX, startingY, platformTop, secondsToGrab, secondsToStand;
        public float climbStaminaCost, hangStaminaCost, pullUpStaminaCost, totalStaminaCost, remainingStamina;
        public float grabbedX, grabbedY, expectedLandingX, landingX, landingY, landingSoleY, horizontalError, feetHeightError;
    }

    [Serializable]
    public sealed class Measurements
    {
        public string editorVersion, measuredUtc, sourceScene, sourceName;
        public float fixedStep = Dt;
        public float sourceSpeed, sourceMaximumSpeed, sourceJumpPower, sourceJumpCooldown;
        public float sourceMass, sourceGravityScale, sourceLinearDamping;
        public float standingWidth, standingHeight, crouchedWidth, crouchedHeight, standingSoleOffset, crouchedSoleOffset;
        public float walkingSpeed, sprintingSpeed, crawlingSpeed, sprintDrainPerSecond, crawlDrainPerSecond;
        public float walkingMinimumSpeed, walkingMaximumSpeed, sprintingMinimumSpeed, sprintingMaximumSpeed, crawlingMinimumSpeed, crawlingMaximumSpeed;
        public JumpMeasurement walkingJump, sprintingJump, walkingDoubleJump, sprintingDoubleJump;
        public LedgeMeasurement leftLedge, rightLedge;
        public bool standingRejectedUnderRoof, crouchReleaseStayedCrouched, jumpRejectedUnderRoof, crawledThroughTunnel, stoodAfterTunnel;
        public bool wallContactWasNotGround, ordinaryWallDidNotClimb, blockedLedgeRejected;
        public float maximumAcceptedLedgeHeightAboveFeet, minimumRejectedLedgeHeightAboveFeet;
        public float maximumAcceptedLedgeFrontGap, minimumRejectedLedgeFrontGap;
        public float ledgeLimitProbeStep = .05f;
        public bool ledgeHeightLimitBracketed, ledgeReachLimitBracketed;
        public bool downReleasedLedge, hangingExhaustionDropped, hangingExhaustionDidNotRegrab;
        public bool triggerLedgeIgnored;
        public float hangingExhaustionRemainingStamina, hangingExhaustionVelocityY;
        public float climbSpeed, climbDrainPerSecond;
        public float climbExhaustionDrop, climbExhaustionVelocityY;
        public float climbExhaustionRemainingStamina;
        public string climbExhaustionState;
        public bool climbExhaustionDropped, walkingAvailableAtExhaustion;
        public float staminaAfterDelayOnly, staminaAfterRecovery, recoveryBaseline;
        public List<ClimbExitMeasurement> climbExits = new List<ClimbExitMeasurement>();
        public List<Check> tests = new List<Check>();
        public List<string> productFailures = new List<string>();
        public List<string> harnessErrors = new List<string>();
    }

    static Phase3MovementChecks() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (running || !EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(Request)) return;
        string requestContent;
        try
        {
            // The writer may still own the request. Retry on the next editor tick.
            using (var stream = new FileStream(Request, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var reader = new StreamReader(stream)) requestContent = reader.ReadToEnd();
            File.Delete(Request);
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        running = true;
        bool climbExitOnly = requestContent.Trim().Equals("climb-exit", StringComparison.OrdinalIgnoreCase);
        string outputPath = climbExitOnly ? Path.Combine("Backups", "Phase3", "climb-exit-measurements.json") : Output;
        var report = new Measurements { editorVersion = Application.unityVersion, measuredUtc = DateTime.UtcNow.ToString("O") };
        try { if (climbExitOnly) RunClimbExits(report); else Run(report); }
        catch (Exception e) { report.harnessErrors.Add("Run: " + e); }
        finally
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
                Debug.Log("Phase 3 movement measurements written: " + outputPath + "; product failures=" + report.productFailures.Count + "; harness errors=" + report.harnessErrors.Count);
            }
            catch (Exception e) { Debug.LogError("Could not write movement measurements: " + e); }
            running = false;
        }
    }

    static void Run(Measurements r)
    {
        Player source = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (source == null) throw new InvalidOperationException("No active scene Player exists; load the actual gameplay scene first.");
        if (source.IsCrouched) throw new InvalidOperationException("Source Player is crouched; run from a fresh standing gameplay spawn to preserve its standing geometry.");
        r.sourceScene = source.gameObject.scene.name; r.sourceName = source.name;
        r.sourceSpeed = source.Speed; r.sourceMaximumSpeed = source.MaxSpeed;
        r.sourceJumpPower = source.JumpPower; r.sourceJumpCooldown = source.jumpCooldown;
        var sourceBody = source.GetComponent<Rigidbody2D>();
        r.sourceMass = sourceBody.mass; r.sourceGravityScale = sourceBody.gravityScale; r.sourceLinearDamping = sourceBody.linearDamping;

        RunFixture(r, source, "flat walk", f =>
        {
            f.Floor(); f.Settle();
            Bounds standing = f.ActorBounds();
            r.standingWidth = standing.size.x; r.standingHeight = standing.size.y;
            r.standingSoleOffset = standing.min.y - f.Body.position.y;
            r.walkingSpeed = f.MeasureSpeed(new TraversalInput { Horizontal = 1 }, out float unused);
            r.walkingMinimumSpeed = f.LastSpeedMinimum; r.walkingMaximumSpeed = f.LastSpeedMaximum;
            float command = Mathf.Min(source.Speed, source.MaxSpeed);
            Expect(r, "flat walk speed", f.Player.Grounded && f.SpeedIsStable && r.walkingSpeed > command * .5f && r.walkingSpeed <= command * 1.05f,
                "actual=" + r.walkingSpeed + ", command=" + command + ", min=" + r.walkingMinimumSpeed + ", max=" + r.walkingMaximumSpeed);
        });
        RunFixture(r, source, "flat sprint", f =>
        {
            f.Floor(); f.Settle();
            r.sprintingSpeed = f.MeasureSpeed(new TraversalInput { Horizontal = 1, Sprint = true }, out float spent);
            r.sprintingMinimumSpeed = f.LastSpeedMinimum; r.sprintingMaximumSpeed = f.LastSpeedMaximum;
            r.sprintDrainPerSecond = spent;
            float command = Mathf.Min(source.Speed * source.sprintMultiplier, source.MaxSpeed);
            Expect(r, "sprint speed before exhaustion", f.Player.Grounded && f.SpeedIsStable && r.sprintingSpeed > r.walkingSpeed * 2 && r.sprintingSpeed <= command * 1.05f,
                "actual=" + r.sprintingSpeed + ", command=" + command + ", min=" + r.sprintingMinimumSpeed + ", max=" + r.sprintingMaximumSpeed);
            Expect(r, "sprint continuous stamina drain", Near(r.sprintDrainPerSecond, source.staminaSettings.SprintDrainPerSecond), r.sprintDrainPerSecond.ToString("F4"));
        });
        RunFixture(r, source, "flat crawl", f =>
        {
            f.Floor(); f.Settle(); f.Step(new TraversalInput { Crouch = true });
            Bounds crouched = f.ActorBounds();
            r.crouchedWidth = crouched.size.x; r.crouchedHeight = crouched.size.y;
            r.crouchedSoleOffset = crouched.min.y - f.Body.position.y;
            r.crawlingSpeed = f.MeasureSpeed(new TraversalInput { Horizontal = 1, Crouch = true }, out float spent);
            r.crawlingMinimumSpeed = f.LastSpeedMinimum; r.crawlingMaximumSpeed = f.LastSpeedMaximum;
            r.crawlDrainPerSecond = spent;
            float command = Mathf.Min(source.Speed * source.crawlSpeedMultiplier, source.MaxSpeed);
            Expect(r, "crawl speed", f.Player.IsCrouched && f.Player.Grounded && f.SpeedIsStable && r.crawlingSpeed > .01f && r.crawlingSpeed < r.walkingSpeed && r.crawlingSpeed <= command * 1.05f,
                "actual=" + r.crawlingSpeed + ", command=" + command + ", min=" + r.crawlingMinimumSpeed + ", max=" + r.crawlingMaximumSpeed);
            Expect(r, "crawl continuous stamina drain", Near(r.crawlDrainPerSecond, source.staminaSettings.CrawlDrainPerSecond), r.crawlDrainPerSecond.ToString("F4"));
            Expect(r, "crouching preserves compound sole", Mathf.Abs(r.crouchedSoleOffset - r.standingSoleOffset) < .015f, "standing=" + r.standingSoleOffset + ", crouched=" + r.crouchedSoleOffset);
        });

        RunFixture(r, source, "walking jump", f => { r.walkingJump = MeasureJump(r, f, false, false); });
        RunFixture(r, source, "sprinting jump", f => { r.sprintingJump = MeasureJump(r, f, true, false); });
        RunFixture(r, source, "walking double jump", f => { r.walkingDoubleJump = MeasureJump(r, f, false, true); });
        RunFixture(r, source, "sprinting double jump", f => { r.sprintingDoubleJump = MeasureJump(r, f, true, true); });
        if (r.walkingJump != null && r.walkingDoubleJump != null)
            Expect(r, "double jump extends walking ascent and reach", r.walkingDoubleJump.peakRise > r.walkingJump.peakRise && r.walkingDoubleJump.horizontalReach > r.walkingJump.horizontalReach, "measured single and double trajectories");

        RunFixture(r, source, "half unit tunnel", f =>
        {
            f.Floor(); f.Settle();
            f.Step(new TraversalInput { Crouch = true });
            f.Box("Tunnel roof", new Vector2(1, .9f), new Vector2(2, .8f)); // bottom .5, x [0,2]
            f.Body.position = new Vector2(1, f.Body.position.y);
            f.Step(new TraversalInput { Crouch = true });
            r.standingRejectedUnderRoof = !f.Player.CanStandAt(f.Body.position, true);
            float beforeJumpY = f.Body.position.y;
            f.Step(new TraversalInput { Jump = true });
            r.crouchReleaseStayedCrouched = f.Player.IsCrouched;
            r.jumpRejectedUnderRoof = f.Player.IsCrouched && f.Body.linearVelocity.y < .1f && f.Body.position.y <= beforeJumpY + .01f;
            int count = 0;
            float clearX = 2 + r.standingWidth * .5f + .08f;
            while (f.Body.position.x < clearX && count++ < 600) f.Step(new TraversalInput { Horizontal = 1, Crouch = true });
            r.crawledThroughTunnel = f.Body.position.x >= clearX;
            // Expire the earlier buffered jump while retaining the crouched posture.
            for (int i = 0; i < 10; i++) f.Step(new TraversalInput { Crouch = true });
            f.Step(default(TraversalInput));
            r.stoodAfterTunnel = !f.Player.IsCrouched;
            Expect(r, "standing rejected in 0.5 high tunnel", r.standingRejectedUnderRoof, "standing compound height=" + r.standingHeight);
            Expect(r, "releasing crouch under roof stays crouched", r.crouchReleaseStayedCrouched, "clearance checked before expansion");
            Expect(r, "jump rejected under tunnel roof", r.jumpRejectedUnderRoof, "no upward impulse while crouched");
            Expect(r, "crawl through 0.5 high tunnel and stand clear", r.crawledThroughTunnel && r.stoodAfterTunnel, "final x=" + f.Body.position.x + ", crouched height=" + r.crouchedHeight);
        });

        RunFixture(r, source, "left ledge pull-up", f => { r.leftLedge = MeasureLedge(r, f, false); });
        RunFixture(r, source, "right ledge pull-up", f => { r.rightLedge = MeasureLedge(r, f, true); });
        MeasureGrabLimits(r, source);
        RunFixture(r, source, "blocked ledge", f =>
        {
            PrepareLedge(f, false);
            f.Box("Blocked landing roof", new Vector2(1, 1.85f), new Vector2(3, .5f)); // bottom 1.6
            f.Step(new TraversalInput { Horizontal = 1 });
            r.blockedLedgeRejected = f.Player.State != TraversalState.Hanging;
            Expect(r, "blocked landing rejects ledge grab", r.blockedLedgeRejected, "roof bottom=1.6; state=" + f.Player.State);
        });
        RunFixture(r, source, "trigger ledge", f =>
        {
            f.Box("Piso trigger ledge", new Vector2(1, .5f), new Vector2(2, 1)).isTrigger = true;
            f.Body.position = new Vector2(-.31f, .65f); f.Body.linearVelocity = new Vector2(0, -.2f);
            f.Step(new TraversalInput { Horizontal = 1 });
            r.triggerLedgeIgnored = f.Player.State != TraversalState.Hanging && !f.Player.Grounded;
            Expect(r, "Piso trigger cannot support a ledge grab", r.triggerLedgeIgnored, "state=" + f.Player.State + ", grounded=" + f.Player.Grounded);
        });
        RunFixture(r, source, "ledge release down", f =>
        {
            PrepareLedge(f, false); f.Step(new TraversalInput { Horizontal = 1 });
            bool hadHang = f.Player.State == TraversalState.Hanging;
            float y = f.Body.position.y;
            f.Step(new TraversalInput { Vertical = -1 });
            for (int i = 0; i < 8; i++) f.Step(default(TraversalInput));
            r.downReleasedLedge = hadHang && f.Player.State == TraversalState.Airborne && f.Body.position.y < y - .01f;
            Expect(r, "down releases ledge and falls", r.downReleasedLedge, "drop=" + (y - f.Body.position.y));
        });
        RunFixture(r, source, "hanging exhaustion", f =>
        {
            PrepareLedge(f, false); f.Step(new TraversalInput { Horizontal = 1 });
            bool hadHang = f.Player.State == TraversalState.Hanging;
            f.Player.Stamina.TrySpend(Mathf.Max(0, f.Player.Stamina.Current - .4f));
            float y = f.Body.position.y;
            bool regrabbed = false, released = false;
            for (int i = 0; i < 12; i++)
            {
                f.Step(new TraversalInput { Horizontal = 1 });
                if (f.Player.State != TraversalState.Hanging) released = true;
                else if (released) regrabbed = true;
            }
            r.hangingExhaustionRemainingStamina = f.Player.Stamina.Current; r.hangingExhaustionVelocityY = f.Body.linearVelocity.y;
            bool cannotAffordNextTick = !f.Player.Stamina.CanSpend(f.Player.staminaSettings.LedgeHangDrainPerSecond * Dt);
            r.hangingExhaustionDropped = hadHang && released && f.Body.position.y < y - .01f && cannotAffordNextTick && r.hangingExhaustionVelocityY < 0;
            r.hangingExhaustionDidNotRegrab = hadHang && released && !regrabbed;
            Expect(r, "hanging exhaustion drops player", r.hangingExhaustionDropped, "drop=" + (y - f.Body.position.y) + ", velocityY=" + r.hangingExhaustionVelocityY + ", remaining=" + r.hangingExhaustionRemainingStamina);
            Expect(r, "exhausted hanging cannot regrab", r.hangingExhaustionDidNotRegrab, "twelve ticks with horizontal input toward wall");
        });
        RunFixture(r, source, "ordinary wall", f =>
        {
            f.Box("Ordinary tall wall", new Vector2(.5f, 5), new Vector2(.5f, 10));
            f.Body.position = new Vector2(0, 2); f.Body.linearVelocity = new Vector2(0, -.2f);
            bool everGround = false, everClimb = false;
            for (int i = 0; i < 20; i++)
            {
                f.Step(new TraversalInput { Horizontal = 1, Vertical = 1 });
                everGround |= f.Player.Grounded; everClimb |= f.Player.State == TraversalState.Climbing;
            }
            r.wallContactWasNotGround = !everGround;
            r.ordinaryWallDidNotClimb = !everClimb && f.Body.position.y < 2;
            Expect(r, "wall contact is not ground", r.wallContactWasNotGround, "upward ground-normal authority");
            Expect(r, "ordinary wall cannot be climbed", r.ordinaryWallDidNotClimb, "Up held at undesignated wall");
        });
        RunFixture(r, source, "designated climb", f =>
        {
            f.Floor(); f.Settle();
            f.Environment.climbableAreas = new[] { new Bounds(new Vector3(0, 10, 0), new Vector3(1, 20, 1)) };
            float y = f.Body.position.y, stamina = f.Player.Stamina.Current;
            for (int i = 0; i < 50; i++) f.Step(new TraversalInput { Vertical = 1 });
            r.climbSpeed = f.Body.position.y - y; r.climbDrainPerSecond = stamina - f.Player.Stamina.Current;
            Expect(r, "designated climb speed", f.Player.State == TraversalState.Climbing && Near(r.climbSpeed, source.climbSpeed), r.climbSpeed.ToString("F4"));
            Expect(r, "designated climb continuous stamina drain", Near(r.climbDrainPerSecond, source.staminaSettings.ClimbDrainPerSecond), r.climbDrainPerSecond.ToString("F4"));
            f.Player.Stamina.TrySpend(Mathf.Max(0, f.Player.Stamina.Current - .1f));
            y = f.Body.position.y;
            // Observe the drop before the floor catches the actor again.
            for (int i = 0; i < 12; i++) f.Step(new TraversalInput { Vertical = 1 });
            r.climbExhaustionDrop = y - f.Body.position.y; r.climbExhaustionVelocityY = f.Body.linearVelocity.y;
            r.climbExhaustionState = f.Player.State.ToString();
            r.climbExhaustionRemainingStamina = f.Player.Stamina.Current;
            bool cannotAffordNextTick = !f.Player.Stamina.CanSpend(f.Player.staminaSettings.ClimbDrainPerSecond * Dt);
            r.climbExhaustionDropped = cannotAffordNextTick && f.Player.State == TraversalState.Airborne && r.climbExhaustionDrop > .3f && r.climbExhaustionVelocityY < 0;
            Expect(r, "climb exhaustion drops player", r.climbExhaustionDropped, "drop=" + r.climbExhaustionDrop + ", velocityY=" + r.climbExhaustionVelocityY + ", state=" + f.Player.State + ", remaining=" + r.climbExhaustionRemainingStamina);
        });
        RunFixture(r, source, "exhausted walking", f =>
        {
            f.Floor(); f.Settle(); f.Player.Stamina.TrySpend(f.Player.Stamina.Current);
            float x = f.Body.position.x;
            for (int i = 0; i < 50; i++) f.Step(new TraversalInput { Horizontal = 1, Sprint = true });
            r.walkingAvailableAtExhaustion = f.Player.Grounded && f.Body.position.x - x > .5f * Mathf.Min(source.Speed, source.MaxSpeed) && f.Body.linearVelocity.x > 0;
            Expect(r, "walking remains available at zero stamina", r.walkingAvailableAtExhaustion, "distance=" + (f.Body.position.x - x));
        });
        RunFixture(r, source, "grounded recovery delay", f =>
        {
            f.Floor(); f.Settle();
            for (int i = 0; i < 10; i++) f.Step(new TraversalInput { Horizontal = 1, Sprint = true });
            r.recoveryBaseline = f.Player.Stamina.Current;
            int delayTicks = Mathf.Max(0, Mathf.FloorToInt(source.staminaSettings.RecoveryDelayAfterExertion / Dt) - 1);
            for (int i = 0; i < delayTicks; i++) f.Step(default(TraversalInput));
            r.staminaAfterDelayOnly = f.Player.Stamina.Current;
            for (int i = 0; i < 27; i++) f.Step(default(TraversalInput));
            r.staminaAfterRecovery = f.Player.Stamina.Current;
            Expect(r, "recovery waits after exertion", Mathf.Abs(r.staminaAfterDelayOnly - r.recoveryBaseline) < .001f, "before=" + r.recoveryBaseline + ", delay=" + r.staminaAfterDelayOnly);
            Expect(r, "grounded idle then recovers stamina", r.staminaAfterRecovery > r.staminaAfterDelayOnly, "after=" + r.staminaAfterRecovery);
        });
    }

    static void RunClimbExits(Measurements report)
    {
        Player source = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (source == null) throw new InvalidOperationException("No active scene Player exists for climb-exit measurements.");
        if (source.IsCrouched) throw new InvalidOperationException("Source Player must have its standing geometry for climb-exit measurements.");
        report.sourceScene = source.gameObject.scene.name; report.sourceName = source.name;
        foreach (bool mirror in new[] { false, true })
        foreach (float distance in new[] { .31f, .5f })
        {
            string label = (mirror ? "right" : "left") + " climb exit at root distance " + distance;
            RunFixture(report, source, label, f =>
            {
                var floor = f.Floor(); f.Settle();
                const float top = 2;
                var foot = f.Player.GetComponentInChildren<CircleCollider2D>();
                if (foot == null) throw new InvalidOperationException("Climb-exit baseline needs the actual compound foot collider.");
                ColliderDistance2D baselineFoot = foot.Distance(floor);
                if (!baselineFoot.isValid) throw new InvalidOperationException("The standing foot/floor baseline distance is invalid.");
                float baselineCompound = f.MinimumNativeSeparation(out string baselinePair);
                float baselineFeetHeight = f.ActorBounds().min.y;
                bool baselineBoxProbe = f.Player.CanStandAt(f.Body.position, false), baselineFootProbe = f.Player.CanStandAt(f.Body.position, true);
                float facing = mirror ? -1 : 1;
                float x = mirror ? 2 + distance : -distance;
                f.Player.transform.localScale = new Vector3(Mathf.Abs(f.Player.transform.localScale.x) * facing, f.Player.transform.localScale.y, f.Player.transform.localScale.z);
                f.Body.position = new Vector2(x, f.Body.position.y); f.Body.linearVelocity = Vector2.zero;
                var platform = f.Box("Raised climb exit platform", new Vector2(1, top * .5f), new Vector2(2, top));
                f.Environment.climbableAreas = new[] { new Bounds(new Vector3(x, 2, 0), new Vector3(.4f, 4, 1)) };
                var m = new ClimbExitMeasurement {
                    mirrored = mirror, startingX = x, startingY = f.Body.position.y, platformTop = top,
                    noSolidOverlap = true, standingProbeAlwaysClear = true, minimumNativeSeparation = float.MaxValue,
                    baselineFootSeparation = baselineFoot.distance, baselineCompoundSeparation = baselineCompound,
                    baselineCompoundPair = baselinePair, configuredContactOffset = Physics2D.defaultContactOffset,
                    baselineFeetHeight = baselineFeetHeight, baselineStandingBoxProbeClear = baselineBoxProbe,
                    baselineStandingFootProbeClear = baselineFootProbe
                };
                report.climbExits.Add(m);
                float initialStamina = f.Player.Stamina.Current;
                float bodyHalfWidth = f.Player.GetComponent<BoxCollider2D>().bounds.extents.x;
                m.expectedLandingX = (mirror ? 2 : 0) + facing * (.08f + bodyHalfWidth + .06f);
                bool finished = false;
                for (int i = 0; i < 250; i++)
                {
                    TraversalState previous = f.Player.State;
                    float before = f.Player.Stamina.Current;
                    // Hold toward the platform while on the rope; Up initiates the pull from hanging.
                    var input = !m.enteredPullUp ? new TraversalInput { Vertical = 1, Horizontal = facing } : default(TraversalInput);
                    f.Step(input);
                    float spent = Mathf.Max(0, before - f.Player.Stamina.Current);
                    if (f.Player.State == TraversalState.Climbing)
                    {
                        m.enteredClimb = true; m.climbStaminaCost += spent;
                    }
                    if (f.Player.State == TraversalState.Hanging)
                    {
                        if (!m.grabbed) { m.grabbedX = f.Body.position.x; m.grabbedY = f.Body.position.y; m.secondsToGrab = (i + 1) * Dt; }
                        m.grabbed = true; m.hangStaminaCost += spent;
                    }
                    if (previous == TraversalState.Hanging && f.Player.State == TraversalState.PullingUp)
                    {
                        m.enteredPullUp = true;
                        float hangSlice = f.Player.staminaSettings.LedgeHangDrainPerSecond * Dt;
                        m.hangStaminaCost += hangSlice; m.pullUpStaminaCost += spent - hangSlice;
                    }
                    if (previous == TraversalState.Climbing && f.Player.State == TraversalState.Airborne)
                        m.exhaustedBeforeStanding = !f.Player.Stamina.CanSpend(f.Player.staminaSettings.ClimbDrainPerSecond * Dt);
                    ObserveClimbExitGeometry(f, m, (i + 1) * Dt);
                    if (m.enteredPullUp && f.Player.Grounded && f.ActorBounds().min.y >= top - .01f)
                    {
                        m.stoodOnPlatform = !f.Player.IsCrouched;
                        m.secondsToStand = (i + 1) * Dt;
                        finished = true;
                        break;
                    }
                }
                m.stayedStanding = finished;
                if (finished)
                    for (int i = 0; i < 10; i++)
                    {
                        f.Step(default(TraversalInput));
                        m.stayedStanding &= f.Player.Grounded && !f.Player.IsCrouched && f.Player.State == TraversalState.Idle && f.ActorBounds().min.y >= top - .01f;
                        ObserveClimbExitGeometry(f, m, m.secondsToStand + (i + 1) * Dt);
                    }
                m.standingProbeAtLanding = f.Player.CanStandAt(f.Body.position, true);
                m.landingStandingBoxProbeClear = f.Player.CanStandAt(f.Body.position, false);
                m.landingX = f.Body.position.x; m.landingY = f.Body.position.y;
                m.landingSoleY = f.ActorBounds().min.y;
                m.horizontalError = Mathf.Abs(m.landingX - m.expectedLandingX);
                m.feetHeightError = Mathf.Abs(m.landingSoleY - top);
                m.totalStaminaCost = initialStamina - f.Player.Stamina.Current;
                m.remainingStamina = f.Player.Stamina.Current;
                m.excessPenetrationBeyondRestBaseline = Mathf.Max(0, m.baselineCompoundSeparation - m.minimumNativeSeparation);
                ColliderDistance2D landingFoot = foot.Distance(platform);
                if (!landingFoot.isValid) throw new InvalidOperationException("The landing foot/platform distance is invalid.");
                m.landingFootSeparation = landingFoot.distance;
                Expect(report, label + " climbs, grabs, pulls and stands", m.enteredClimb && m.grabbed && m.enteredPullUp && m.stoodOnPlatform && m.stayedStanding,
                    "grab seconds=" + m.secondsToGrab + ", stand seconds=" + m.secondsToStand + ", landed=" + m.landingX + "," + m.landingY);
                Expect(report, label + " pays pull cost without exhaustion", Near(m.pullUpStaminaCost, source.staminaSettings.LedgePullUpCost) && !m.exhaustedBeforeStanding && m.remainingStamina > 0,
                    "climb=" + m.climbStaminaCost + ", hang=" + m.hangStaminaCost + ", pull=" + m.pullUpStaminaCost + ", total=" + m.totalStaminaCost);
                Expect(report, label + " has clear and accurate landing", m.noSolidOverlap && m.horizontalError <= .03f && m.feetHeightError <= .03f,
                    "native min separation=" + m.minimumNativeSeparation + ", baseline foot=" + m.baselineFootSeparation + ", excess below rest=" + m.excessPenetrationBeyondRestBaseline + ", configured contact offset=" + m.configuredContactOffset + ", pair=" + m.minimumNativeSeparationPair + ", phase=" + m.minimumNativeSeparationState + ", horizontal error=" + m.horizontalError + ", feet height error=" + m.feetHeightError);
            });
        }
    }

    static void ObserveClimbExitGeometry(Fixture fixture, ClimbExitMeasurement measurement, float time)
    {
        float separation = fixture.MinimumNativeSeparation(out string pair);
        if (separation < measurement.minimumNativeSeparation)
        {
            measurement.minimumNativeSeparation = separation;
            measurement.minimumNativeSeparationPair = pair;
            measurement.minimumNativeSeparationState = fixture.Player.State.ToString();
            measurement.minimumNativeSeparationTime = time;
        }
        // Native distance includes contact skin even at stable rest. Use the project's existing
        // engine contact tolerance, and export the measured resting baseline for comparison.
        if (separation < -measurement.configuredContactOffset)
        {
            if (measurement.noSolidOverlap)
            {
                measurement.firstInvalidPenetrationTime = time; measurement.firstInvalidPenetrationDistance = separation;
                measurement.firstInvalidPenetrationPair = pair; measurement.firstInvalidPenetrationState = fixture.Player.State.ToString();
            }
            measurement.noSolidOverlap = false;
        }
        bool proposedStanceClear = fixture.Player.CanStandAt(fixture.Body.position, true);
        if (!proposedStanceClear && measurement.standingProbeAlwaysClear)
        {
            measurement.firstStandingProbeFailureTime = time;
            measurement.firstStandingProbeFailureX = fixture.Body.position.x; measurement.firstStandingProbeFailureY = fixture.Body.position.y;
            measurement.firstStandingProbeFailureState = fixture.Player.State.ToString();
            measurement.firstStandingProbeFailureBlocker = fixture.StandingProbeBlocker();
        }
        measurement.standingProbeAlwaysClear &= proposedStanceClear;
    }

    static JumpMeasurement MeasureJump(Measurements report, Fixture f, bool sprint, bool secondJump)
    {
        f.Floor(); f.Settle();
        var input = new TraversalInput { Horizontal = 1, Sprint = sprint };
        for (int i = 0; i < 20; i++) f.Step(input);
        var m = new JumpMeasurement { sprintHeld = sprint, secondPressSeconds = secondJump ? .24f : -1 };
        Vector2 origin = f.Body.position;
        m.samples.Add(new TrajectorySample());
        float stamina = f.Player.Stamina.Current;
        for (int i = 0; i < 250; i++)
        {
            input.Jump = i == 0 || (secondJump && i == 12); // second press at .24 s, independently of a guessed trajectory
            float before = f.Player.Stamina.Current;
            f.Step(input);
            if (secondJump && i == 12)
            {
                float continuous = sprint ? f.Player.staminaSettings.SprintDrainPerSecond * Dt : 0;
                m.secondJumpStaminaCost = before - f.Player.Stamina.Current - continuous;
                m.secondJumpAccepted = Near(m.secondJumpStaminaCost, f.Player.staminaSettings.DoubleJumpCost);
            }
            m.peakRise = Mathf.Max(m.peakRise, f.Body.position.y - origin.y);
            m.flightSeconds = (i + 1) * Dt;
            m.samples.Add(new TrajectorySample { time = m.flightSeconds, x = f.Body.position.x - origin.x, y = f.Body.position.y - origin.y });
            if (i > 2 && f.Player.Grounded && f.Body.linearVelocity.y <= .1f) { m.landed = true; break; }
        }
        m.horizontalReach = f.Body.position.x - origin.x; m.staminaSpent = stamina - f.Player.Stamina.Current;
        string name = (sprint ? "sprinting" : "walking") + (secondJump ? " double jump" : " single jump");
        Expect(report, name + " completes measured trajectory", m.landed && m.peakRise > .05f && m.horizontalReach > .05f, "peak=" + m.peakRise + ", flight=" + m.flightSeconds + ", reach=" + m.horizontalReach + ", stamina=" + m.staminaSpent);
        if (secondJump) Expect(report, name + " pays second-jump cost", m.secondJumpAccepted, "discrete cost=" + m.secondJumpStaminaCost);
        return m;
    }

    static void PrepareLedge(Fixture f, bool mirror)
    {
        f.Box("Ledge platform", new Vector2(1, .5f), new Vector2(2, 1));
        f.Player.transform.localScale = new Vector3(Mathf.Abs(f.Player.transform.localScale.x) * (mirror ? -1 : 1), f.Player.transform.localScale.y, f.Player.transform.localScale.z);
        f.Body.position = new Vector2(mirror ? 2.31f : -.31f, .65f);
        f.Body.linearVelocity = new Vector2(0, -.2f);
    }

    static void MeasureGrabLimits(Measurements report, Player source)
    {
        var heightRejections = new List<float>();
        var gapRejections = new List<float>();
        bool heightAccepted = false, gapAccepted = false;
        for (int i = 0; i <= 10; i++)
        {
            float top = .8f + i * .05f;
            RunFixture(report, source, "ledge height probe " + top, f =>
            {
                f.Box("Height probe ledge", new Vector2(1, top * .5f), new Vector2(2, top));
                f.Body.position = new Vector2(-.31f, .65f); f.Body.linearVelocity = new Vector2(0, -.2f);
                f.Step(new TraversalInput { Horizontal = 1 });
                // The native bounds now reflect the simulated actor. Undo its first-tick displacement
                // to report the initial feet height rather than the height after a rejected fall.
                float sole = f.ActorBounds().min.y - f.Body.position.y;
                float aboveFeet = top - (.65f + sole);
                if (f.Player.State == TraversalState.Hanging)
                {
                    heightAccepted = true;
                    report.maximumAcceptedLedgeHeightAboveFeet = Mathf.Max(report.maximumAcceptedLedgeHeightAboveFeet, aboveFeet);
                }
                else heightRejections.Add(aboveFeet);
            });
        }
        report.minimumRejectedLedgeHeightAboveFeet = float.MaxValue;
        foreach (float rejected in heightRejections)
            if (rejected > report.maximumAcceptedLedgeHeightAboveFeet)
                report.minimumRejectedLedgeHeightAboveFeet = Mathf.Min(report.minimumRejectedLedgeHeightAboveFeet, rejected);
        report.ledgeHeightLimitBracketed = heightAccepted && report.minimumRejectedLedgeHeightAboveFeet < float.MaxValue;
        if (!report.ledgeHeightLimitBracketed) report.minimumRejectedLedgeHeightAboveFeet = 0;

        for (int i = 0; i <= 8; i++)
        {
            float distance = .31f + i * .05f;
            RunFixture(report, source, "ledge reach probe " + distance, f =>
            {
                f.Box("Reach probe ledge", new Vector2(1, .5f), new Vector2(2, 1));
                f.Body.position = new Vector2(-distance, .65f); f.Body.linearVelocity = new Vector2(0, -.2f);
                f.Step(new TraversalInput { Horizontal = 1 });
                float frontOffset = f.ActorBounds().max.x - f.Body.position.x;
                float gap = distance - frontOffset;
                if (f.Player.State == TraversalState.Hanging)
                {
                    gapAccepted = true;
                    report.maximumAcceptedLedgeFrontGap = Mathf.Max(report.maximumAcceptedLedgeFrontGap, gap);
                }
                else gapRejections.Add(gap);
            });
        }
        report.minimumRejectedLedgeFrontGap = float.MaxValue;
        foreach (float rejected in gapRejections)
            if (rejected > report.maximumAcceptedLedgeFrontGap)
                report.minimumRejectedLedgeFrontGap = Mathf.Min(report.minimumRejectedLedgeFrontGap, rejected);
        report.ledgeReachLimitBracketed = gapAccepted && report.minimumRejectedLedgeFrontGap < float.MaxValue;
        if (!report.ledgeReachLimitBracketed) report.minimumRejectedLedgeFrontGap = 0;
        Expect(report, "ledge grab height limit is measured and bracketed", report.ledgeHeightLimitBracketed,
            "accepted above feet=" + report.maximumAcceptedLedgeHeightAboveFeet + ", first greater rejection=" + report.minimumRejectedLedgeHeightAboveFeet + ", step=.05");
        Expect(report, "ledge grab horizontal reach is measured and bracketed", report.ledgeReachLimitBracketed,
            "accepted front gap=" + report.maximumAcceptedLedgeFrontGap + ", first greater rejection=" + report.minimumRejectedLedgeFrontGap + ", step=.05");
    }

    static LedgeMeasurement MeasureLedge(Measurements report, Fixture f, bool mirror)
    {
        PrepareLedge(f, mirror);
        var m = new LedgeMeasurement();
        f.Step(new TraversalInput { Horizontal = mirror ? -1 : 1 });
        m.grabbed = f.Player.State == TraversalState.Hanging;
        m.hangX = f.Body.position.x; m.hangY = f.Body.position.y;
        float before = f.Player.Stamina.Current;
        f.Step(new TraversalInput { Jump = true });
        m.enteredPullUp = f.Player.State == TraversalState.PullingUp;
        m.pullUpStaminaCost = before - f.Player.Stamina.Current - f.Player.staminaSettings.LedgeHangDrainPerSecond * Dt;
        bool movedUp = false, movedAcross = false, crossedTooEarly = false;
        float targetY = 1 - f.Player.SoleOffset + .025f;
        for (int i = 0; i < 150; i++)
        {
            f.Step(default(TraversalInput));
            movedUp |= f.Body.position.y > m.hangY + .1f;
            bool across = Mathf.Abs(f.Body.position.x - m.hangX) > .025f;
            movedAcross |= across;
            if (across && f.Body.position.y < targetY - .035f) crossedTooEarly = true;
            m.pullUpSeconds = (i + 1) * Dt;
            if (f.Player.Grounded && f.Player.State != TraversalState.PullingUp)
            {
                m.landed = f.Body.position.x > 0 && f.Body.position.x < 2 && f.ActorBounds().min.y > .97f;
                break;
            }
        }
        m.verticalThenHorizontal = movedUp && movedAcross && !crossedTooEarly;
        m.landingX = f.Body.position.x; m.landingY = f.Body.position.y;
        string side = mirror ? "right" : "left";
        Expect(report, side + " ledge grabs while falling", m.grabbed, "hang=" + m.hangX + "," + m.hangY);
        Expect(report, side + " ledge pull-up uses two segments and lands", m.enteredPullUp && m.verticalThenHorizontal && m.landed, "land=" + m.landingX + "," + m.landingY + "; time=" + m.pullUpSeconds);
        Expect(report, side + " ledge pull-up discrete stamina cost", m.enteredPullUp && Near(m.pullUpStaminaCost, f.Player.staminaSettings.LedgePullUpCost), m.pullUpStaminaCost.ToString("F4"));
        return m;
    }

    static bool Near(float a, float b) { return Mathf.Abs(a - b) <= Mathf.Max(.025f, Mathf.Abs(b) * .025f); }
    static void Expect(Measurements r, string name, bool passed, string detail)
    {
        r.tests.Add(new Check { name = name, passed = passed, detail = detail });
        if (!passed) r.productFailures.Add(name + ": " + detail);
    }

    static void RunFixture(Measurements r, Player source, string label, Action<Fixture> test)
    {
        Fixture fixture = null;
        try { fixture = new Fixture(source); test(fixture); }
        catch (Exception e) { r.harnessErrors.Add(label + ": " + e); }
        finally { if (fixture != null) fixture.Dispose(); }
    }

    sealed class Fixture : IDisposable
    {
        readonly Scene previousScene;
        readonly Scene scene;
        readonly PhysicsScene2D physics;
        readonly List<Collider2D> solids = new List<Collider2D>();
        public readonly Player Player;
        public readonly Rigidbody2D Body;
        public readonly TraversalEnvironment Environment;
        public float LastSpeedMinimum { get; private set; }
        public float LastSpeedMaximum { get; private set; }
        public bool SpeedIsStable { get { return LastSpeedMaximum - LastSpeedMinimum <= Mathf.Max(.025f, LastSpeedMaximum * .05f); } }

        public Fixture(Player source)
        {
            previousScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Phase3Movement_" + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
            try
            {
                SceneManager.SetActiveScene(scene);
                var staging = new GameObject("Inactive fixture staging");
                staging.SetActive(false);
                var actor = UnityEngine.Object.Instantiate(source.gameObject, staging.transform, false);
                actor.name = "Measured scene Player";
                actor.transform.position = new Vector3(0, 3, 0);
                Player = actor.GetComponent<Player>();
                Player.enabled = false;
                foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != Player) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                Body = actor.GetComponent<Rigidbody2D>();
                Body.linearVelocity = Vector2.zero; Body.angularVelocity = 0;
                var environmentObject = new GameObject("Nonlethal measurement environment");
                Environment = environmentObject.AddComponent<TraversalEnvironment>();
                Environment.bottomVoidEnabled = false; Environment.voidAreas = new Bounds[0];
                staging.SetActive(true); // Awake sees actual geometry in its final local physics scene.
                Player.ConfigureTraversal(Environment);
                if (Player.Stamina == null) throw new InvalidOperationException("Player Awake did not initialize the inactive clone.");
            }
            catch
            {
                Cleanup();
                throw;
            }
            finally { if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene); }
        }

        public BoxCollider2D Box(string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = 10; go.transform.position = center;
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = size; solids.Add(collider);
            return collider;
        }

        public float MinimumNativeSeparation(out string pair)
        {
            float minimum = float.MaxValue; pair = "";
            foreach (var actor in Player.GetComponentsInChildren<Collider2D>())
            {
                if (!actor.enabled || actor.isTrigger || actor.attachedRigidbody != Body) continue;
                foreach (var solid in solids)
                {
                    if (!solid.enabled || solid.isTrigger) continue;
                    ColliderDistance2D distance = actor.Distance(solid);
                    if (!distance.isValid) throw new InvalidOperationException("Invalid native separation for " + actor.name + " and " + solid.name);
                    if (distance.distance < minimum)
                    {
                        minimum = distance.distance;
                        pair = actor.name + "/" + actor.GetType().Name + " vs " + solid.name + "/" + solid.GetType().Name;
                    }
                }
            }
            if (minimum == float.MaxValue) throw new InvalidOperationException("No valid actor/terrain pair was measured.");
            return minimum;
        }

        // Diagnostic copy of the current standing probe, solely to identify a false result's shape/blocker.
        public string StandingProbeBlocker()
        {
            var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(Player.collisionLayer);
            var results = new Collider2D[16];
            var box = Player.GetComponent<BoxCollider2D>();
            Vector2 scale = new Vector2(Mathf.Abs(Player.transform.lossyScale.x), Mathf.Abs(Player.transform.lossyScale.y));
            Vector2 center = Body.position + (Vector2)Player.transform.TransformVector(box.offset);
            int count = physics.OverlapBox(center, Vector2.Scale(box.size, scale) - Vector2.one * .01f, Player.transform.eulerAngles.z, filter, results);
            for (int i = 0; i < count; i++) if (results[i].attachedRigidbody != Body)
                return "standing box vs " + results[i].name + "; center=" + center + ", size=" + Vector2.Scale(box.size, scale);
            var foot = Player.GetComponentInChildren<CircleCollider2D>();
            if (foot != null)
            {
                Vector2 offset = (Vector2)foot.transform.TransformPoint(foot.offset) - (Vector2)Player.transform.position;
                float radius = foot.radius * Mathf.Max(Mathf.Abs(foot.transform.lossyScale.x), Mathf.Abs(foot.transform.lossyScale.y)) - .005f;
                count = physics.OverlapCircle(Body.position + offset, radius, filter, results);
                for (int i = 0; i < count; i++) if (results[i].attachedRigidbody != Body)
                    return "standing foot vs " + results[i].name + "; query center=" + (Body.position + offset) + ", radius=" + radius + ", native bounds=" + foot.bounds;
            }
            return "probe became clear when repeated";
        }

        public BoxCollider2D Floor() { return Box("Flat floor layer 10", new Vector2(0, -.5f), new Vector2(200, 1)); }

        public void Settle()
        {
            Body.position = new Vector2(0, -Player.SoleOffset + .035f);
            Body.linearVelocity = Vector2.zero;
            for (int i = 0; i < 50; i++) Step(default(TraversalInput));
            if (!Player.Grounded) throw new InvalidOperationException("Fixture did not settle on its layer-10 floor. Check actual Player collision mask and source colliders.");
        }

        public void Step(TraversalInput input)
        {
            Player.SampleTraversalInput(input);
            Player.SimulateTraversal(Dt);
            if (!physics.Simulate(Dt)) throw new InvalidOperationException("Local PhysicsScene2D simulation failed.");
        }

        public float MeasureSpeed(TraversalInput input, out float spent)
        {
            float stamina = Player.Stamina.Current, fromX = 0;
            LastSpeedMinimum = float.PositiveInfinity; LastSpeedMaximum = float.NegativeInfinity;
            for (int i = 0; i < 50; i++)
            {
                if (i == 25) fromX = Body.position.x;
                float beforeX = Body.position.x;
                Step(input);
                if (i >= 25)
                {
                    float speed = (Body.position.x - beforeX) / Dt;
                    LastSpeedMinimum = Mathf.Min(LastSpeedMinimum, speed); LastSpeedMaximum = Mathf.Max(LastSpeedMaximum, speed);
                }
            }
            spent = stamina - Player.Stamina.Current;
            return (Body.position.x - fromX) / .5f;
        }

        public Bounds ActorBounds()
        {
            bool initialized = false; var bounds = new Bounds();
            foreach (var collider in Player.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != Body) continue;
                if (!initialized) { bounds = collider.bounds; initialized = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (!initialized) throw new InvalidOperationException("No enabled physical Player collider was measured.");
            return bounds;
        }

        void Cleanup()
        {
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            SceneManager.UnloadSceneAsync(scene);
        }

        public void Dispose() { Cleanup(); }
    }
}
#endif
