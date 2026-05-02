using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runtime ragdoll for enemy death.
/// - Disables AI and attack trigger scripts.
/// - Builds a stable chain of rigidbodies/joints.
/// - Falls back to root tumble if skeleton data is insufficient.
/// </summary>
public static class EnemyDeathRagdoll
{
    private const float JointSwingLimit = 10f;
    private const float JointTwistLimit = 5f;
    private const float BoneDrag = 2.4f;
    private const float BoneAngularDrag = 18.0f;
    private const float BoneMaxAngularVelocity = 1.2f;
    private const float UpperBodyExtraAngularDrag = 10.0f;
    private const float UpperBodyMaxAngularVelocity = 0.8f;
    private const float MinCapsuleHeight = 0.14f;

    public static void Activate(Animator animator, NavMeshAgent agent, Rigidbody rootBody, CapsuleCollider rootCapsule)
    {
        Transform root = rootBody != null ? rootBody.transform : animator.transform.root;

        if (animator != null)
        {
            animator.speed = 0f;
            animator.Update(0.0001f);
        }

        if (agent != null)
        {
            agent.enabled = false;
            Object.Destroy(agent);
        }

        foreach (sphereTriggerDamage trigger in root.GetComponentsInChildren<sphereTriggerDamage>(true))
            Object.Destroy(trigger);

        // Keep root capsule enabled so the body doesn't sink into the ground.
        if (rootCapsule != null)
            rootCapsule.enabled = true;

        // Root RB was used for NavMesh movement. On death we must make it dynamic,
        // otherwise the hips (connected to root) get pinned in world space.
        if (rootBody != null)
        {
            rootBody.linearVelocity = Vector3.zero;
            rootBody.angularVelocity = Vector3.zero;
            rootBody.isKinematic = false;
            rootBody.useGravity = true;
            rootBody.constraints = RigidbodyConstraints.None;
            rootBody.linearDamping = BoneDrag;
            rootBody.angularDamping = BoneAngularDrag;
            rootBody.maxAngularVelocity = BoneMaxAngularVelocity;
        }

        bool built = animator != null && BuildHumanoidRagdoll(animator, rootBody);
        if (!built && animator != null)
            built = BuildFromSkinnedBones(animator, rootBody);
        if (!built)
            FallbackTumble(rootBody, rootCapsule);

        if (animator != null)
            animator.enabled = false;

        WakeRagdollBodies(root, rootBody);
        IgnoreRootCapsuleVsRagdoll(root, rootCapsule, rootBody);
        AddDeathImpulse(animator, root, rootBody);
    }

    /// <summary>
    /// Простая смерть: без костей. Враг ложится плашмя и остаётся как один rigidbody+capsule.
    /// </summary>
    public static void ActivateFlatFall(Animator animator, NavMeshAgent agent, Rigidbody rootBody, CapsuleCollider rootCapsule)
    {
        Transform root = rootBody != null ? rootBody.transform : animator.transform.root;

        if (animator != null)
        {
            animator.enabled = false;
        }

        if (agent != null)
        {
            agent.enabled = false;
            Object.Destroy(agent);
        }

        foreach (sphereTriggerDamage trigger in root.GetComponentsInChildren<sphereTriggerDamage>(true))
            Object.Destroy(trigger);

        if (rootCapsule != null)
            rootCapsule.enabled = true;

        if (rootBody == null)
            return;

        rootBody.constraints = RigidbodyConstraints.FreezeRotation;
        rootBody.isKinematic = false;
        rootBody.useGravity = true;
        rootBody.linearDamping = BoneDrag;
        rootBody.angularDamping = BoneAngularDrag;
        rootBody.maxAngularVelocity = BoneMaxAngularVelocity;

        // Lay flat: rotate so forward points downward, keep yaw.
        Vector3 forward = root.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Quaternion yaw = Quaternion.LookRotation(forward, Vector3.up);
        root.rotation = yaw * Quaternion.Euler(90f, 0f, 0f);

        // Small settle impulse.
        rootBody.AddForce(Vector3.down * 0.2f, ForceMode.VelocityChange);
    }

    /// <summary>
    /// Частичный регдолл: туловище + ноги + только плечо/верхняя часть руки (без локтя/предплечья/пальцев).
    /// </summary>
    public static void ActivatePartial(Animator animator, NavMeshAgent agent, Rigidbody rootBody, CapsuleCollider rootCapsule)
    {
        Transform root = rootBody != null ? rootBody.transform : animator.transform.root;

        if (animator != null)
        {
            animator.speed = 0f;
            animator.Update(0.0001f);
        }

        if (agent != null)
        {
            agent.enabled = false;
            Object.Destroy(agent);
        }

        foreach (sphereTriggerDamage trigger in root.GetComponentsInChildren<sphereTriggerDamage>(true))
            Object.Destroy(trigger);

        if (rootCapsule != null)
            rootCapsule.enabled = true;

        if (rootBody != null)
        {
            rootBody.linearVelocity = Vector3.zero;
            rootBody.angularVelocity = Vector3.zero;
            rootBody.isKinematic = false;
            rootBody.useGravity = true;
            rootBody.constraints = RigidbodyConstraints.None;
            rootBody.linearDamping = BoneDrag;
            rootBody.angularDamping = BoneAngularDrag;
            rootBody.maxAngularVelocity = BoneMaxAngularVelocity;
        }

        bool built = animator != null && BuildPartialHumanoid(animator, rootBody);
        if (!built && animator != null)
            built = BuildPartialSkinned(animator, rootBody);
        if (!built)
            ActivateFlatFall(animator, agent, rootBody, rootCapsule);
        else
        {
            if (animator != null)
                animator.enabled = false;
            WakeRagdollBodies(root, rootBody);
            TunePartialElasticity(root, rootBody);
            IgnoreRootCapsuleVsRagdoll(root, rootCapsule, rootBody);
            AddDeathImpulse(animator, root, rootBody);
        }
    }

    private static bool BuildPartialHumanoid(Animator anim, Rigidbody rootBody)
    {
        if (rootBody == null || anim.avatar == null || !anim.avatar.isHuman || !anim.avatar.isValid)
            return false;

        var map = new Dictionary<Transform, Rigidbody>();

        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
            return false;
        Rigidbody hipsRb = AddBone(hips, rootBody, map, 7f);

        Transform spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        Rigidbody spineRb = spine != null ? AddBone(spine, hipsRb, map, 5f) : hipsRb;

        Transform chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
        Rigidbody chestRb = chest != null ? AddBone(chest, spineRb, map, 4f) : spineRb;

        // Arms: only upper arms (no forearms/elbows, no hands/fingers).
        Transform lUpperArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (lUpperArm != null)
            AddBone(lUpperArm, chestRb, map, 2f);

        Transform rUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (rUpperArm != null)
            AddBone(rUpperArm, chestRb, map, 2f);

        // Legs: upper + lower legs, no feet/toes.
        AddLimb(anim, map, hipsRb, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 3.5f, 2f);
        AddLimb(anim, map, hipsRb, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 3.5f, 2f);

        return map.Count >= 5;
    }

    private static bool BuildPartialSkinned(Animator anim, Rigidbody rootBody)
    {
        if (rootBody == null)
            return false;

        SkinnedMeshRenderer[] renderers = anim.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (renderers.Length == 0)
            return false;

        var uniqueBones = new HashSet<Transform>();
        foreach (SkinnedMeshRenderer r in renderers)
        foreach (Transform b in r.bones)
        {
            if (b == null || !IsChildOf(b, anim.transform))
                continue;
            uniqueBones.Add(b);
        }

        if (uniqueBones.Count == 0)
            return false;

        Transform hips = null;
        Transform spine = null;
        Transform chest = null;
        Transform lUpperArm = null;
        Transform rUpperArm = null;
        Transform lUpperLeg = null;
        Transform lLowerLeg = null;
        Transform rUpperLeg = null;
        Transform rLowerLeg = null;

        foreach (Transform t in uniqueBones)
        {
            string n = t.name.ToLowerInvariant();

            if (hips == null && (n.Contains("hip") || n.Contains("pelvis")))
                hips = t;
            else if (spine == null && n.Contains("spine"))
                spine = t;
            else if (chest == null && (n.Contains("chest") || n.Contains("upperchest")))
                chest = t;
            else if (lUpperArm == null && IsLeft(n) && (n.Contains("upperarm") || n.Contains("shoulder") || n.Contains("arm_l")))
                lUpperArm = t;
            else if (rUpperArm == null && IsRight(n) && (n.Contains("upperarm") || n.Contains("shoulder") || n.Contains("arm_r")))
                rUpperArm = t;
            else if (lUpperLeg == null && IsLeft(n) && (n.Contains("thigh") || n.Contains("upperleg") || n.Contains("upleg")))
                lUpperLeg = t;
            else if (lLowerLeg == null && IsLeft(n) && (n.Contains("calf") || n.Contains("lowerleg") || n.Contains("leg")))
                lLowerLeg = t;
            else if (rUpperLeg == null && IsRight(n) && (n.Contains("thigh") || n.Contains("upperleg") || n.Contains("upleg")))
                rUpperLeg = t;
            else if (rLowerLeg == null && IsRight(n) && (n.Contains("calf") || n.Contains("lowerleg") || n.Contains("leg")))
                rLowerLeg = t;
        }

        if (hips == null)
            return false;

        var map = new Dictionary<Transform, Rigidbody>();
        Rigidbody hipsRb = AddBone(hips, rootBody, map, 7f);
        Rigidbody spineRb = spine != null ? AddBone(spine, hipsRb, map, 5f) : hipsRb;
        Rigidbody chestRb = chest != null ? AddBone(chest, spineRb, map, 4f) : spineRb;

        if (lUpperArm != null) AddBone(lUpperArm, chestRb, map, 2f);
        if (rUpperArm != null) AddBone(rUpperArm, chestRb, map, 2f);

        if (lUpperLeg != null)
        {
            Rigidbody lUpperLegRb = AddBone(lUpperLeg, hipsRb, map, 3.5f);
            if (lLowerLeg != null && lLowerLeg != lUpperLeg)
                AddBone(lLowerLeg, lUpperLegRb, map, 2f);
        }

        if (rUpperLeg != null)
        {
            Rigidbody rUpperLegRb = AddBone(rUpperLeg, hipsRb, map, 3.5f);
            if (rLowerLeg != null && rLowerLeg != rUpperLeg)
                AddBone(rLowerLeg, rUpperLegRb, map, 2f);
        }

        return map.Count >= 4;
    }

    private static bool BuildHumanoidRagdoll(Animator anim, Rigidbody rootBody)
    {
        if (rootBody == null || anim.avatar == null || !anim.avatar.isHuman || !anim.avatar.isValid)
            return false;

        var map = new Dictionary<Transform, Rigidbody>();

        Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
            return false;
        Rigidbody hipsRb = AddBone(hips, rootBody, map, 7f);

        Transform spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        Rigidbody spineRb = spine != null ? AddBone(spine, hipsRb, map, 5f) : hipsRb;

        Transform chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
        Rigidbody chestRb = chest != null ? AddBone(chest, spineRb, map, 4f) : spineRb;

        Transform neck = anim.GetBoneTransform(HumanBodyBones.Neck);
        Rigidbody neckRb = neck != null ? AddBone(neck, chestRb, map, 2f) : chestRb;

        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        if (head != null)
            AddBone(head, neckRb, map, 2.5f);

        AddLimb(anim, map, chestRb, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 2f, 1.2f);
        AddLimb(anim, map, chestRb, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 2f, 1.2f);
        AddLimb(anim, map, hipsRb, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 3.5f, 2f);
        AddLimb(anim, map, hipsRb, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 3.5f, 2f);

        return map.Count >= 6;
    }

    private static void AddLimb(Animator anim, Dictionary<Transform, Rigidbody> map, Rigidbody attachTo,
        HumanBodyBones upper, HumanBodyBones lower, float upperMass, float lowerMass)
    {
        Transform upperTf = anim.GetBoneTransform(upper);
        if (upperTf == null)
            return;

        Rigidbody upperRb = AddBone(upperTf, attachTo, map, upperMass);
        Transform lowerTf = anim.GetBoneTransform(lower);
        if (lowerTf != null)
            AddBone(lowerTf, upperRb, map, lowerMass);
    }

    private static bool BuildFromSkinnedBones(Animator anim, Rigidbody rootBody)
    {
        if (rootBody == null)
            return false;

        SkinnedMeshRenderer[] renderers = anim.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (renderers.Length == 0)
            return false;

        var uniqueBones = new HashSet<Transform>();
        foreach (SkinnedMeshRenderer r in renderers)
        foreach (Transform b in r.bones)
        {
            if (b == null || IsExcludedBoneName(b.name))
                continue;
            if (!IsChildOf(b, anim.transform))
                continue;
            uniqueBones.Add(b);
        }

        if (uniqueBones.Count < 8)
            return false;

        var sorted = new List<Transform>(uniqueBones);
        sorted.Sort((a, b) => DepthFromRoot(a, anim.transform).CompareTo(DepthFromRoot(b, anim.transform)));

        var map = new Dictionary<Transform, Rigidbody>();
        foreach (Transform bone in sorted)
        {
            if (bone == anim.transform)
                continue;

            Rigidbody connected = FindClosestParentRb(bone.parent, anim.transform, rootBody, map);
            float mass = bone.name.ToLowerInvariant().Contains("hip") ? 5f : 1.2f;
            AddBone(bone, connected, map, mass);
        }

        return map.Count >= 8;
    }

    private static Rigidbody AddBone(Transform bone, Rigidbody connectedBody, Dictionary<Transform, Rigidbody> registry, float mass)
    {
        foreach (Collider c in bone.GetComponents<Collider>())
            if (!c.isTrigger)
                Object.Destroy(c);

        Rigidbody rb = bone.GetComponent<Rigidbody>();
        if (rb == null)
            rb = bone.gameObject.AddComponent<Rigidbody>();

        rb.mass = Mathf.Max(mass, 0.2f);
        rb.isKinematic = true;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = BoneDrag;
        rb.angularDamping = BoneAngularDrag;
        rb.maxAngularVelocity = BoneMaxAngularVelocity;
        rb.solverIterations = 14;
        rb.solverVelocityIterations = 6;
        rb.sleepThreshold = 0.02f;

        // Upper body (arms/head) tends to jitter on short segments.
        if (IsUpperBodyBoneName(bone.name))
        {
            rb.angularDamping = BoneAngularDrag + UpperBodyExtraAngularDrag;
            rb.maxAngularVelocity = UpperBodyMaxAngularVelocity;
        }

        CapsuleCollider capsule = bone.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        ConfigureCapsuleAlongChild(bone, capsule);

        CharacterJoint joint = bone.GetComponent<CharacterJoint>();
        if (joint == null)
            joint = bone.gameObject.AddComponent<CharacterJoint>();

        joint.connectedBody = connectedBody;
        joint.autoConfigureConnectedAnchor = true;
        joint.enableCollision = false;
        // Too aggressive projection on small bones causes visible jitter.
        joint.enableProjection = true;
        joint.enablePreprocessing = true;
        joint.projectionAngle = 2.0f;
        joint.projectionDistance = 0.02f;

        joint.lowTwistLimit = new SoftJointLimit { limit = -JointTwistLimit };
        joint.highTwistLimit = new SoftJointLimit { limit = JointTwistLimit };
        joint.swing1Limit = new SoftJointLimit { limit = JointSwingLimit };
        joint.swing2Limit = new SoftJointLimit { limit = JointSwingLimit };

        // IMPORTANT: do NOT disable all self-collisions (it makes the body collapse into itself).
        // Instead, ignore only the direct connected pair to prevent initial penetration explosion.
        if (connectedBody != null)
        {
            // ignore with any collider on the connected body object
            foreach (Collider parentCol in connectedBody.GetComponents<Collider>())
            {
                if (parentCol != null && !parentCol.isTrigger)
                    Physics.IgnoreCollision(capsule, parentCol, true);
            }
        }

        registry[bone] = rb;
        return rb;
    }

    private static void ConfigureCapsuleAlongChild(Transform bone, CapsuleCollider capsule)
    {
        Transform child = bone.childCount > 0 ? bone.GetChild(0) : null;
        Vector3 localTo = child != null ? bone.InverseTransformPoint(child.position) : Vector3.up * 0.2f;
        if (localTo.sqrMagnitude < 0.0001f)
            localTo = Vector3.up * 0.2f;

        // Choose major axis of the bone->child vector in local space.
        int axis = 1; // Y
        float ax = Mathf.Abs(localTo.x);
        float ay = Mathf.Abs(localTo.y);
        float az = Mathf.Abs(localTo.z);
        if (ax > ay && ax > az) axis = 0;
        else if (az > ay && az > ax) axis = 2;

        // Use signed component along selected axis so center stays on that axis.
        float comp = axis == 0 ? localTo.x : axis == 1 ? localTo.y : localTo.z;
        float length = Mathf.Max(Mathf.Abs(comp), MinCapsuleHeight);

        float radius = Mathf.Clamp(length * 0.22f, 0.03f, 0.11f);
        float height = Mathf.Max(length, MinCapsuleHeight);
        // Unity requires height >= 2*radius (otherwise behaves unpredictably).
        height = Mathf.Max(height, radius * 2f + 0.02f);

        capsule.direction = axis;
        capsule.radius = radius;
        capsule.height = height;

        // Put capsule between bone and child strictly along chosen axis (avoid sideways offsets -> penetrations).
        Vector3 center = Vector3.zero;
        float half = comp * 0.5f;
        if (axis == 0) center.x = half;
        else if (axis == 1) center.y = half;
        else center.z = half;
        capsule.center = center;
    }

    private static void WakeRagdollBodies(Transform root, Rigidbody rootBody)
    {
        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>())
        {
            if (rb == rootBody)
                continue;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = BoneDrag;
            rb.angularDamping = BoneAngularDrag;
            rb.maxAngularVelocity = BoneMaxAngularVelocity;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 14;
            rb.solverVelocityIterations = 6;
        }
    }

    private static void IgnoreRootCapsuleVsRagdoll(Transform root, CapsuleCollider rootCapsule, Rigidbody rootBody)
    {
        // Root capsule blocks ground penetration; ignore it vs ragdoll parts to avoid jitter.
        if (rootCapsule == null)
            return;

        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in cols)
        {
            if (c == null || c.isTrigger)
                continue;
            if (c == rootCapsule)
                continue;
            Physics.IgnoreCollision(rootCapsule, c, true);
        }
    }

    private static void TunePartialElasticity(Transform root, Rigidbody rootBody)
    {
        // Slightly more spring-like behavior only for PartialRagdoll.
        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>())
        {
            if (rb == rootBody)
                continue;

            rb.angularDamping = Mathf.Max(4.5f, rb.angularDamping * 0.58f);
            rb.maxAngularVelocity = Mathf.Min(2.8f, rb.maxAngularVelocity * 1.6f);
        }

        foreach (CharacterJoint joint in root.GetComponentsInChildren<CharacterJoint>())
        {
            SoftJointLimit low = joint.lowTwistLimit;
            SoftJointLimit high = joint.highTwistLimit;
            SoftJointLimit s1 = joint.swing1Limit;
            SoftJointLimit s2 = joint.swing2Limit;

            low.limit = Mathf.Max(-28f, low.limit - 7f);
            high.limit = Mathf.Min(28f, high.limit + 7f);
            s1.limit = Mathf.Min(26f, s1.limit + 8f);
            s2.limit = Mathf.Min(26f, s2.limit + 8f);

            joint.lowTwistLimit = low;
            joint.highTwistLimit = high;
            joint.swing1Limit = s1;
            joint.swing2Limit = s2;

            joint.projectionAngle = Mathf.Min(8f, joint.projectionAngle * 2.0f);
            joint.projectionDistance = Mathf.Min(0.06f, joint.projectionDistance * 2.6f);
        }
    }

    private static void AddDeathImpulse(Animator anim, Transform root, Rigidbody rootBody)
    {
        Rigidbody target = null;
        if (anim != null && anim.avatar != null && anim.avatar.isHuman)
        {
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
                hips.TryGetComponent(out target);
        }

        if (target == null)
        {
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>())
            {
                if (rb == rootBody)
                    continue;
                target = rb;
                break;
            }
        }

        if (target != null)
            target.AddForce(Vector3.up * 0.05f, ForceMode.VelocityChange);
    }

    private static void FallbackTumble(Rigidbody rootBody, CapsuleCollider rootCapsule)
    {
        if (rootBody == null)
            return;

        if (rootCapsule != null)
            rootCapsule.enabled = true;

        rootBody.isKinematic = false;
        rootBody.useGravity = true;
        rootBody.constraints = RigidbodyConstraints.None;
        rootBody.interpolation = RigidbodyInterpolation.Interpolate;
        rootBody.AddTorque(Random.insideUnitSphere * 4f + Vector3.forward, ForceMode.VelocityChange);
    }

    private static Rigidbody FindClosestParentRb(Transform current, Transform animatorRoot, Rigidbody rootBody,
        Dictionary<Transform, Rigidbody> map)
    {
        while (current != null)
        {
            if (current == animatorRoot)
                return rootBody;
            if (map.TryGetValue(current, out Rigidbody rb))
                return rb;
            current = current.parent;
        }
        return rootBody;
    }

    private static bool IsExcludedBoneName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("finger") || n.Contains("thumb") || n.Contains("toe") || n.Contains("eye") || n.Contains("ik");
    }

    private static bool IsLeft(string n) =>
        n.Contains("left") || n.EndsWith("_l") || n.StartsWith("l_") || n.Contains(".l");

    private static bool IsRight(string n) =>
        n.Contains("right") || n.EndsWith("_r") || n.StartsWith("r_") || n.Contains(".r");

    private static bool IsUpperBodyBoneName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("head") || n.Contains("neck") || n.Contains("spine")
               || n.Contains("chest") || n.Contains("clav") || n.Contains("shoulder")
               || n.Contains("arm") || n.Contains("hand") || n.Contains("wrist");
    }

    private static bool IsChildOf(Transform child, Transform root)
    {
        Transform p = child;
        while (p != null)
        {
            if (p == root)
                return true;
            p = p.parent;
        }
        return false;
    }

    private static int DepthFromRoot(Transform bone, Transform root)
    {
        int d = 0;
        Transform p = bone;
        while (p != null && p != root)
        {
            d++;
            p = p.parent;
        }
        return d;
    }
}