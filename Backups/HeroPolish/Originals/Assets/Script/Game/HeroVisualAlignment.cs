using UnityEngine;

// One visual renderer follows the physics-owned pose; never moves the Rigidbody or colliders.
[DisallowMultipleComponent]
public sealed class HeroVisualAlignment : MonoBehaviour
{
    SpriteRenderer visual;
    TraversalState previousState;
    int climbHalfCycle;
    float plantedHandY;
    float handTransferFromY, handTransfer = 1;
    public SpriteRenderer Visual { get { return visual; } }

    public void Apply(Player player, SpriteRenderer source, Animator animator, TraversalEnvironment environment, float deltaTime)
    {
        if (source.sprite == null) return;
        if (visual == null)
        {
            var child = new GameObject("Hero artwork", typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            visual = child.GetComponent<SpriteRenderer>();
        }
        visual.sprite = source.sprite; visual.color = source.color; visual.sharedMaterial = source.sharedMaterial;
        visual.sortingLayerID = source.sortingLayerID; visual.sortingOrder = source.sortingOrder;
        visual.flipX = source.flipX; visual.flipY = source.flipY;
        visual.transform.localScale = Vector3.one;
        Vector3 position = Vector3.zero;
        if (player.IsCrouched || player.Grounded)
            position.y = player.SoleOffset / Mathf.Abs(transform.lossyScale.y) - source.sprite.bounds.min.y;
        if (player.State == TraversalState.Hanging || player.State == TraversalState.PullingUp)
        {
            Vector3 anchor = player.GripAnchor; anchor.z = transform.position.z;
            position = transform.InverseTransformPoint(anchor);
            if (player.State == TraversalState.PullingUp)
            {
                // Hands bear weight first; once the knee/foot plants, hand off to the standing sole.
                float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, 1, player.PullUpProgress));
                var standing = new Vector3(0, player.SoleOffset / Mathf.Abs(transform.lossyScale.y) - source.sprite.bounds.min.y, 0);
                position = Vector3.Lerp(position, standing, t);
            }
        }
        else if (player.State == TraversalState.Climbing && environment != null)
        {
            int layer = animator.GetLayerIndex("Traversal Visuals");
            float cycle = layer >= 0 ? animator.GetCurrentAnimatorStateInfo(layer).normalizedTime : 0;
            int half = Mathf.FloorToInt(cycle * 2);
            if (previousState != TraversalState.Climbing || half != climbHalfCycle)
            {
                // Alternate load-bearing hands. A stopped climb retains its contact rather than sliding.
                handTransferFromY = previousState == TraversalState.Climbing ? visual.transform.position.y : transform.position.y + .32f;
                handTransfer = previousState == TraversalState.Climbing ? 0 : 1;
                plantedHandY = transform.position.y + .32f;
                climbHalfCycle = half;
            }
            if (animator.GetFloat("ClimbPlayback") > .001f)
                handTransfer = Mathf.Min(1, handTransfer + Mathf.Max(0, deltaTime) / .1f);
            float handY = Mathf.Lerp(handTransferFromY, plantedHandY, Mathf.SmoothStep(0, 1, handTransfer));
            foreach (Bounds area in environment.climbableAreas)
            {
                if (!area.Contains(new Vector3(transform.position.x, transform.position.y, area.center.z))) continue;
                position = transform.InverseTransformPoint(new Vector3(area.center.x, handY, transform.position.z));
                break;
            }
        }
        visual.transform.localPosition = position;
        visual.enabled = true; source.enabled = false; previousState = player.State;
    }
    void OnDisable()
    {
        if (visual != null) visual.enabled = false;
        var source = GetComponent<SpriteRenderer>(); if (source != null) source.enabled = true;
    }
}
