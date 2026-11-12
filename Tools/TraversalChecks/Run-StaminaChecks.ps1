param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\..\Assets\Script\Game\TraversalStamina.cs'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\Backups\Phase3\stamina-checks.json')
)

$ErrorActionPreference = 'Stop'
$priorFailure = $null
if (Test-Path -LiteralPath $OutputPath) {
    $previous = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
    if ($previous.testFirstEvidence) { $priorFailure = $previous.testFirstEvidence }
    elseif ($previous.passed -eq $false) { $priorFailure = $previous.failure }
}
$report = [ordered]@{
    passed = $false
    verification = 'PowerShell Add-Type: pure C# stamina contract checks; not a Unity compilation or playtest'
    testFirstEvidence = $priorFailure
}

try {
    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw 'Missing stamina API: TraversalStamina.cs does not exist.'
    }
    $checks = @'
public static class StaminaContractChecks
{
    public static int Assertions;

    private static void Check(bool condition, string description)
    {
        Assertions++;
        if (!condition) throw new Exception(description);
    }

    private static void Near(float actual, float expected, string description)
    {
        Check(!float.IsNaN(actual) && Math.Abs(actual - expected) < 0.0001f,
            description + ": expected " + expected + ", got " + actual);
    }

    private static void Reject(Action action, string description)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, description);
    }

    private static void InvalidSetting(Action<StaminaSettings> edit, string description)
    {
        StaminaSettings settings = new StaminaSettings();
        edit(settings);
        Reject(() => settings.Validate(), "Validate rejects " + description);
        Reject(() => new TraversalStamina(settings), "Constructor rejects " + description);
    }

    public static void Run()
    {
        StaminaSettings settings = new StaminaSettings();
        Check(Attribute.IsDefined(typeof(StaminaSettings), typeof(SerializableAttribute)), "Serializable tuning data");
        settings.Validate();
        TraversalStamina stamina = new TraversalStamina(settings);
        Near(stamina.Current, 100f, "New resource starts at prototype maximum");
        Near(stamina.Maximum, 100f, "Prototype maximum");
        Near(stamina.RecoveryRemaining, 0f, "No initial exertion delay");
        Near(settings.SprintDrainPerSecond, 20f, "Prototype sprint rate");
        Near(settings.ClimbDrainPerSecond, 30f, "Prototype climb rate");
        Near(settings.LedgeHangDrainPerSecond, 14f, "Prototype hang rate");
        Near(settings.CrawlDrainPerSecond, 8f, "Prototype crawl rate");
        Near(settings.DoubleJumpCost, 18f, "Prototype double jump cost");
        Near(settings.LedgePullUpCost, 15f, "Prototype pull-up cost");
        Near(settings.GroundIdleRecoveryPerSecond, 13f, "Prototype idle recovery");
        Near(settings.WalkingRecoveryPerSecond, 5f, "Prototype walking recovery");
        Near(settings.RecoveryDelayAfterExertion, 1.5f, "Prototype exertion delay");

        Check(stamina.CanSpend(18f), "Double jump initially affordable");
        Check(stamina.TrySpend(18f), "Double jump is paid atomically");
        Near(stamina.Current, 82f, "Double jump consumes prototype cost");
        Near(stamina.RecoveryRemaining, 1.5f, "Spend starts recovery delay");
        stamina.Recover(0.5f, true, false, false);
        Near(stamina.RecoveryRemaining, 1f, "Rest advances cooldown once");
        Check(!stamina.CanSpend(83f), "Insufficient spend detected without mutation");
        Check(!stamina.TrySpend(83f), "Insufficient spend rejected");
        Near(stamina.Current, 82f, "Rejected spend preserves stamina");
        Near(stamina.RecoveryRemaining, 1f, "Rejected spend preserves delay");
        Check(stamina.TrySpend(0f), "Zero-cost action is affordable");
        Near(stamina.RecoveryRemaining, 1f, "Zero cost does not refresh delay");

        stamina = new TraversalStamina(settings);
        Check(stamina.Drain(settings.SprintDrainPerSecond, 2f), "Two-second sprint retains stamina");
        Near(stamina.Current, 60f, "Continuous sprint drain");
        stamina.Recover(0.02f, true, false, true);
        Near(stamina.Current, 60f, "No healing during exertion");
        Near(stamina.RecoveryRemaining, 1.5f, "Exertion tick does not double-advance delay");
        Check(!stamina.Drain(settings.ClimbDrainPerSecond, 3f), "Long climb reports exhausted");
        Near(stamina.Current, 0f, "Continuous drain clamps at zero");
        Check(!stamina.Drain(14f, 1f), "Exhausted hanging remains exhausted");
        Near(stamina.Current, 0f, "Exhaustion does not underflow");
        Check(stamina.CanSpend(0f), "Zero-cost walking remains possible at exhaustion");
        Check(stamina.TrySpend(0f), "Resource does not block zero-cost walking");
        stamina.Recover(8f, false, false, false);
        Near(stamina.Current, 0f, "Exhausted airborne player never heals");
        Near(stamina.RecoveryRemaining, 0f, "Air time can elapse exertion delay");
        stamina.Recover(1f, true, true, false);
        Near(stamina.Current, 5f, "Walking heals at five per second after air delay");
        stamina.Recover(1f, true, false, false);
        Near(stamina.Current, 18f, "Idle heals at thirteen per second");
        stamina.Recover(100f, true, false, false);
        Near(stamina.Current, 100f, "Recovery caps at maximum");

        TraversalStamina singleTick = new TraversalStamina(settings);
        TraversalStamina splitTicks = new TraversalStamina(settings);
        singleTick.TrySpend(50f);
        splitTicks.TrySpend(50f);
        singleTick.Recover(2f, true, false, false);
        for (int i = 0; i < 8; i++) splitTicks.Recover(0.25f, true, false, false);
        Near(singleTick.Current, 56.5f, "Only post-delay fraction heals");
        Near(splitTicks.Current, 56.5f, "Partitioned recovery matches single tick");
        Near(splitTicks.RecoveryRemaining, 0f, "Partitioned cooldown completes once");
        TraversalStamina walk = new TraversalStamina(settings);
        walk.TrySpend(50f);
        walk.Recover(2f, true, true, false);
        Near(walk.Current, 52.5f, "Walking recovery uses only post-delay fraction");

        TraversalStamina switches = new TraversalStamina(settings);
        switches.Drain(14f, 1f);
        switches.Recover(0.1f, false, false, true);
        switches.Recover(0.1f, false, true, false);
        switches.Recover(0.1f, true, true, true);
        Near(switches.Current, 86f, "Air/ground/movement transitions do not refill");
        Near(switches.RecoveryRemaining, 1.4f, "Only nonexerting transition advances delay");
        switches.Drain(0f, 1f);
        switches.Drain(5f, 0f);
        Near(switches.RecoveryRemaining, 1.4f, "Zero drain does not refresh delay");
        switches.Drain(8f, 0.5f);
        Near(switches.Current, 82f, "Crawl uses continuous resource");
        Near(switches.RecoveryRemaining, 1.5f, "New actual drain refreshes delay");
        Check(switches.TrySpend(15f), "Pull-up discrete cost succeeds");
        Near(switches.Current, 67f, "Pull-up consumes fifteen");
        Check(switches.TrySpend(67f), "Exact remaining cost succeeds");
        Near(switches.Current, 0f, "Exact spend reaches zero");

        StaminaSettings free = new StaminaSettings {
            SprintDrainPerSecond = 0f, ClimbDrainPerSecond = 0f,
            LedgeHangDrainPerSecond = 0f, CrawlDrainPerSecond = 0f,
            DoubleJumpCost = 0f, LedgePullUpCost = 0f,
            GroundIdleRecoveryPerSecond = 0f, WalkingRecoveryPerSecond = 0f,
            RecoveryDelayAfterExertion = 0f
        };
        free.Validate();
        TraversalStamina zeroRecovery = new TraversalStamina(free);
        zeroRecovery.TrySpend(10f);
        zeroRecovery.Recover(100f, true, false, false);
        Near(zeroRecovery.Current, 90f, "Zero recovery rate is valid");
        TraversalStamina immediate = new TraversalStamina(new StaminaSettings { RecoveryDelayAfterExertion = 0f });
        immediate.TrySpend(50f);
        immediate.Recover(1f, true, false, false);
        Near(immediate.Current, 63f, "Zero delay recovers immediately");

        InvalidSetting(s => s.MaxStamina = 0f, "zero maximum");
        InvalidSetting(s => s.MaxStamina = -1f, "negative maximum");
        System.Reflection.FieldInfo[] fields = typeof(StaminaSettings).GetFields();
        for (int i = 0; i < fields.Length; i++)
        {
            System.Reflection.FieldInfo field = fields[i];
            InvalidSetting(s => field.SetValue(s, -1f), "negative " + field.Name);
            InvalidSetting(s => field.SetValue(s, float.NaN), "NaN " + field.Name);
            InvalidSetting(s => field.SetValue(s, float.PositiveInfinity), "positive infinite " + field.Name);
            InvalidSetting(s => field.SetValue(s, float.NegativeInfinity), "negative infinite " + field.Name);
        }
        Reject(() => new TraversalStamina(null), "Reject null configuration");
        float[] invalid = { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        for (int i = 0; i < invalid.Length; i++)
        {
            float value = invalid[i];
            Reject(() => switches.CanSpend(value), "Reject invalid affordability amount");
            Reject(() => switches.TrySpend(value), "Reject invalid discrete cost");
            Reject(() => switches.Drain(value, 1f), "Reject invalid drain rate");
            Reject(() => switches.Drain(1f, value), "Reject invalid drain timestep");
            Reject(() => switches.Recover(value, true, false, false), "Reject invalid recovery timestep");
        }
        TraversalStamina large = new TraversalStamina(settings);
        large.Drain(float.MaxValue, float.MaxValue);
        Near(large.Current, 0f, "Large finite drain remains bounded");
        large.Recover(float.MaxValue, true, false, false);
        Near(large.Current, 100f, "Large finite recovery remains bounded");
    }
}
'@
    Add-Type -TypeDefinition ((Get-Content -LiteralPath $SourcePath -Raw) + [Environment]::NewLine + $checks) -Language CSharp
    [StaminaContractChecks]::Run()
    $report.passed = $true
    $report.assertions = [StaminaContractChecks]::Assertions
    $report.checks = @('prototype tuning', 'atomic spend/rejection', 'continuous exhaustion', 'zero-cost walking at zero', 'delayed idle/walking recovery', 'partitioned cooldown', 'air/exertion blocks healing', 'transition persistence', 'invalid settings/inputs', 'large finite arithmetic')
}
catch {
    $report.failure = $_.Exception.Message
    if ($report.failure -like 'Missing stamina API:*') { $report.testFirstEvidence = $report.failure }
    throw
}
finally {
    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not (Test-Path -LiteralPath $outputDirectory)) { New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null }
    $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    $report | ConvertTo-Json -Depth 4 | Write-Output
}
