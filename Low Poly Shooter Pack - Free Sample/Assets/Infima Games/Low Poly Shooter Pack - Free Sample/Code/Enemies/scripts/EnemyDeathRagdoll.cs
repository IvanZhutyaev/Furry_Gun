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
    private const float JointSwingLimit = 28f;
    private const float JointTwistLimit = 18f;
    private const float BoneDrag = 0.8f;
    private const float BoneAngularDrag = 6.0f;
    private const float BoneMaxAngularVelocity = 3.0f;
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

        if (rootCapsule != null)
            rootCapsule.enabled = false;

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
        IgnoreSelfCollisions(root, rootBody);
        AddDeathImpulse(animator, root, rootBody);
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
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = BoneDrag;
        rb.angularDamping = BoneAngularDrag;
        rb.maxAngularVelocity = BoneMaxAngularVelocity;

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
        joint.enableProjection = true;
        joint.enablePreprocessing = true;
        joint.projectionAngle = 8f;
        joint.projectionDistance = 0.03f;

        joint.lowTwistLimit = new SoftJointLimit { limit = -JointTwistLimit };
        joint.highTwistLimit = new SoftJointLimit { limit = JointTwistLimit };
        joint.swing1Limit = new SoftJointLimit { limit = JointSwingLimit };
        joint.swing2Limit = new SoftJointLimit { limit = JointSwingLimit };

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

        float radius = Mathf.Clamp(length * 0.18f, 0.025f, 0.085f);
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
        }
    }

    private static void IgnoreSelfCollisions(Transform root, Rigidbody rootBody)
    {
        // If ragdoll parts collide with each other, they often explode due to initial penetration.
        // We disable self-collisions for all ragdoll colliders.
        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        List<Collider> ragCols = new List<Collider>(cols.Length);
        foreach (Collider c in cols)
        {
            if (c == null || c.isTrigger)
                continue;
            // Don't include the old root capsule if it was re-enabled in fallback.
            if (rootBody != null && c.attachedRigidbody == rootBody)
                continue;
            ragCols.Add(c);
        }

        for (int i = 0; i < ragCols.Count; i++)
        {
            for (int j = i + 1; j < ragCols.Count; j++)
                Physics.IgnoreCollision(ragCols[i], ragCols[j], true);
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
            target.AddForce(Vector3.up * 0.15f, ForceMode.VelocityChange);
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