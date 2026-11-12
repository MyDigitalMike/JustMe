using System;

[Serializable]
public sealed class StaminaSettings
{
    public float MaxStamina = 100f;
    public float SprintDrainPerSecond = 20f;
    public float ClimbDrainPerSecond = 30f;
    public float LedgeHangDrainPerSecond = 14f;
    public float CrawlDrainPerSecond = 8f;
    public float DoubleJumpCost = 18f;
    public float LedgePullUpCost = 15f;
    public float GroundIdleRecoveryPerSecond = 13f;
    public float WalkingRecoveryPerSecond = 5f;
    public float RecoveryDelayAfterExertion = 1.5f;

    public void Validate()
    {
        ValidateNonnegative(MaxStamina, nameof(MaxStamina));
        if (MaxStamina == 0f)
            throw new ArgumentOutOfRangeException(nameof(MaxStamina), "Maximum stamina must be positive.");
        ValidateNonnegative(SprintDrainPerSecond, nameof(SprintDrainPerSecond));
        ValidateNonnegative(ClimbDrainPerSecond, nameof(ClimbDrainPerSecond));
        ValidateNonnegative(LedgeHangDrainPerSecond, nameof(LedgeHangDrainPerSecond));
        ValidateNonnegative(CrawlDrainPerSecond, nameof(CrawlDrainPerSecond));
        ValidateNonnegative(DoubleJumpCost, nameof(DoubleJumpCost));
        ValidateNonnegative(LedgePullUpCost, nameof(LedgePullUpCost));
        ValidateNonnegative(GroundIdleRecoveryPerSecond, nameof(GroundIdleRecoveryPerSecond));
        ValidateNonnegative(WalkingRecoveryPerSecond, nameof(WalkingRecoveryPerSecond));
        ValidateNonnegative(RecoveryDelayAfterExertion, nameof(RecoveryDelayAfterExertion));
    }

    internal static void ValidateNonnegative(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and nonnegative.");
    }
}

/// <summary>Shared traversal resource. The owner calls Recover once per simulation tick.</summary>
public sealed class TraversalStamina
{
    private readonly float maximum;
    private readonly float idleRecovery;
    private readonly float walkingRecovery;
    private readonly float recoveryDelay;
    private float current;
    private float recoveryRemaining;

    public float Current { get { return current; } }
    public float Maximum { get { return maximum; } }
    public float RecoveryRemaining { get { return recoveryRemaining; } }

    public TraversalStamina(StaminaSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.Validate();
        maximum = settings.MaxStamina;
        idleRecovery = settings.GroundIdleRecoveryPerSecond;
        walkingRecovery = settings.WalkingRecoveryPerSecond;
        recoveryDelay = settings.RecoveryDelayAfterExertion;
        current = maximum;
    }

    public bool CanSpend(float amount)
    {
        StaminaSettings.ValidateNonnegative(amount, nameof(amount));
        return amount <= current;
    }

    public bool TrySpend(float amount)
    {
        if (!CanSpend(amount)) return false;
        if (amount > 0f)
        {
            current -= amount;
            recoveryRemaining = recoveryDelay;
        }
        return true;
    }

    public bool Drain(float perSecond, float deltaTime)
    {
        StaminaSettings.ValidateNonnegative(perSecond, nameof(perSecond));
        StaminaSettings.ValidateNonnegative(deltaTime, nameof(deltaTime));
        float previous = current;
        current = (float)Math.Max(0d, current - perSecond * (double)deltaTime);
        if (current < previous) recoveryRemaining = recoveryDelay;
        return current > 0f;
    }

    public void Recover(float deltaTime, bool grounded, bool walking, bool exerting)
    {
        StaminaSettings.ValidateNonnegative(deltaTime, nameof(deltaTime));
        // Spending/draining already starts the full delay for this tick.
        if (exerting) return;

        double recoveryTime = Math.Max(0d, (double)deltaTime - recoveryRemaining);
        recoveryRemaining = (float)Math.Max(0d, (double)recoveryRemaining - deltaTime);
        if (!grounded || recoveryTime == 0d) return;

        float rate = walking ? walkingRecovery : idleRecovery;
        current = (float)Math.Min(maximum, current + rate * recoveryTime);
    }
}
