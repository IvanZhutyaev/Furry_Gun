using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// После гибели: отключает агента, убирает триггер удара по игроку, переводит тело в физический регдолл.
/// При humanoid‑аватаре добавляются суставы по ключевым костям; иначе — по всем костям SkinnedMeshRenderer (упрощённо); иначе — падающий корень с капсулой.
/// </summary>
public static class EnemyDeathRagdoll
{
    public static void Activate(Animator animator, NavMeshAgent agent, Rigidbody rootBody,
        CapsuleCollider rootCapsule)
    {
        Transform rootTf = rootBody != null ? rootBody.transform : animator.transform.root;

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

        foreach (var dmg in rootTf.GetComponentsInChildren<sphereTriggerDamage>(true))
            Object.Destroy(dmg);

        if (rootCapsule != null)
            rootCapsule.enabled = false;

        if (rootBody != null)
        {
            rootBody.linearVelocity = Vector3.zero;
            rootBody.angularVelocity = Vector3.zero;
            rootBody.isKinematic = true;
            rootBody.useGravity = false;
        }

        bool ok = animator != null && TryHumanoid(animator, rootBody);
        if (!ok && animator != null)
            ok = TrySkinned(animator, rootBody);
        if (!ok)
            FallbackTumble(rootBody, rootCapsule);

        if (animator != null)
            animator.enabled = false;

        WakeBodies(rootTf, rootBody);
        BumpImpulse(animator, rootTf, rootBody);
    }

    private static bool TryHumanoid(Animator anim, Rigidbody rootRb)
    {
        if (rootRb == null || anim.avatar == null || !anim.avatar.isHuman || !anim.avatar.isValid)
            return false;

        Transform hipsTf = anim.GetBoneTransform(HumanBodyBones.Hips);
        if (hipsTf == null)
            return false;

        var map = new Dictionary<Transform, Rigidbody>();

        Rigidbody rbHips = AddBone(hipsTf, rootRb, map, 8f, 0.38f);

        Rigidbody spineRb = rbHips;
        Transform spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        if (spine != null && spine != hipsTf)
            spineRb = AddBone(spine, rbHips, map, 6f, 0.26f);

        Rigidbody chestRb = spineRb;
        Transform chestLike = anim.GetBoneTransform(HumanBodyBones.UpperChest)
                              ?? anim.GetBoneTransform(HumanBodyBones.Chest);
        if (chestLike != null && chestLike != spine && chestLike != hipsTf)
            chestRb = AddBone(chestLike, spineRb, map, 6f, 0.21f);

        Rigidbody neckRb = chestRb;
        Transform neck = anim.GetBoneTransform(HumanBodyBones.Neck);
        if (neck != null)
            neckRb = AddBone(neck, chestRb, map, 2f, 0.11f);

        Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
        if (head != null)
            AddBone(head, neckRb, map, 2.8f, 0.13f);

        LimbPair(anim, map, chestRb, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 2.4f, 1.3f, 0.1f,
            0.07f);

        LimbPair(anim, map, chestRb, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 2.4f, 1.3f, 0.1f,

            0.07f);

        LimbPair(anim, map, rbHips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 4f, 2f, 0.12f, 0.08f);

        LimbPair(anim, map, rbHips, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 4f, 2f, 0.12f,

            0.08f);

        return map.Count >= 5;
    }

    private static void LimbPair(Animator anim, Dictionary<Transform, Rigidbody> map, Rigidbody attachRb,
        HumanBodyBones upper, HumanBodyBones lower, float massUpper, float massLower,
        float rUpper, float rLower)

    {

        Transform u = anim.GetBoneTransform(upper);

        Transform l = anim.GetBoneTransform(lower);

        if (u == null || attachRb == null)
            return;

        Rigidbody ru = AddBone(u, attachRb, map, massUpper, rUpper);

        if (l != null && ru != null)
            AddBone(l, ru, map, massLower, rLower);

    }

    private static bool TrySkinned(Animator anim, Rigidbody rootRb)
    {
        if (rootRb == null)
            return false;

        SkinnedMeshRenderer[] smrs = anim.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (smrs.Length == 0)

            return false;

        List<Transform> bones = CollectBones(smrs, anim.transform);
        if (bones.Count < 8)

            return false;


        bones.Sort((a, b) => HierarchyDepth(a, anim.transform).CompareTo(HierarchyDepth(b, anim.transform)));

        Dictionary<Transform, Rigidbody> map = new Dictionary<Transform, Rigidbody>();

        foreach (Transform bone in bones)

        {

            if (bone == anim.transform)
                continue;

            if (bone.parent == null)
                continue;

            Rigidbody conn = FindParentRb(bone.parent, anim.transform, rootRb, map);
            float m = bone.name.IndexOf("hip", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 5f : 1.2f;
            AddBone(bone, conn, map, m, 0.1f);
        }



        return map.Count >= 6;
    }



    private static List<Transform> CollectBones(SkinnedMeshRenderer[] smrs, Transform animTf)
    {


        HashSet<Transform> set = new HashSet<Transform>();
        foreach (var sm in smrs)
            foreach (Transform b in sm.bones)


            {


                if (b == null || BoneNameExcluded(b.name))
                    continue;
                if (!IsUnderHierarchy(b, animTf))
                    continue;
                set.Add(b);



            }



        List<Transform> list = new List<Transform>();

        foreach (Transform t in set)
            list.Add(t);

        return list;


    }



    private static Rigidbody FindParentRb(Transform t, Transform animTf, Rigidbody rootRb,
        Dictionary<Transform, Rigidbody> map)



    {


        while (t != null)
        {


            if (t == animTf)
                break;
            if (map.TryGetValue(t, out Rigidbody rb))
                return rb;
            t = t.parent;


        }



        return rootRb;


    }



    private static bool BoneNameExcluded(string n)

    {


        string ln = n.ToLowerInvariant();


        return ln.Contains("finger") || ln.Contains("toe") || ln.Contains("thumb")
               || ln.Contains("index") || ln.Contains("middle") || ln.Contains("ring")


               || ln.Contains("pinky") || ln.Contains("eye") || ln.Contains(" ik") || ln.Contains("ik");


    }



    private static bool IsUnderHierarchy(Transform bone, Transform root)

    {


        Transform p = bone;

        while (p != null)
        {


            if (p == root)
                return true;
            p = p.parent;



        }



        return false;


    }



    private static int HierarchyDepth(Transform bone, Transform animatorRoot)



    {


        int d = 0;
        Transform p = bone;
        while (p != null && p != animatorRoot)
        {


            d++;
            p = p.parent;



        }



        return d;


    }



    private static Rigidbody AddBone(Transform bone, Rigidbody connectTo,
        Dictionary<Transform, Rigidbody> registry, float mass, float sphereRadius)

    {


        foreach (Collider c in bone.GetComponents<Collider>())
        {


            if (!c.isTrigger)
                Object.Destroy(c);



        }



        Rigidbody rb = bone.gameObject.GetComponent<Rigidbody>();
        if (rb == null)


            rb = bone.gameObject.AddComponent<Rigidbody>();


        rb.mass = Mathf.Max(mass, 0.25f);
        rb.isKinematic = true;
        rb.useGravity = true;



        SphereCollider sphere = bone.gameObject.GetComponent<SphereCollider>();
        if (sphere != null)
            Object.Destroy(sphere);



        sphere = bone.gameObject.AddComponent<SphereCollider>();
        sphere.radius = Mathf.Clamp(sphereRadius, 0.04f, 0.45f);



        CharacterJoint joint = bone.gameObject.GetComponent<CharacterJoint>();
        if (joint == null)
            joint = bone.gameObject.AddComponent<CharacterJoint>();


        joint.connectedBody = connectTo;
        joint.anchor = Vector3.zero;
        joint.autoConfigureConnectedAnchor = true;
        joint.enableCollision = false;
        joint.enableProjection = true;
        joint.enablePreprocessing = true;


        SoftJointLimit lim = new SoftJointLimit { limit = 40f };


        joint.lowTwistLimit = lim;
        joint.highTwistLimit = lim;



        joint.swing1Limit = lim;
        joint.swing2Limit = lim;



        registry[bone] = rb;

        return rb;


    }



    private static void WakeBodies(Transform rootTf, Rigidbody rootRb)

    {


        foreach (Rigidbody rb in rootTf.GetComponentsInChildren<Rigidbody>())
        {


            if (rb == rootRb)



                continue;
            rb.isKinematic = false;
            rb.useGravity = true;


        }


    }



    private static void BumpImpulse(Animator anim, Transform rootTf, Rigidbody rootRb)

    {


        Rigidbody hit = null;
        if (anim != null && anim.avatar != null && anim.avatar.isHuman)
        {


            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)



                hips.TryGetComponent(out hit);


        }


        if (hit == null)
        {


            foreach (Rigidbody rb in rootTf.GetComponentsInChildren<Rigidbody>())
            {


                if (rb == rootRb)
                    continue;



                hit = rb;
                break;



            }



        }



        if (hit != null)
            hit.AddForce(Random.insideUnitSphere * 0.9f + Vector3.up * 0.55f,
                ForceMode.VelocityChange);


    }



    private static void FallbackTumble(Rigidbody rootRb, CapsuleCollider cap)

    {


        if (rootRb == null)
            return;

        rootRb.interpolation = RigidbodyInterpolation.Interpolate;
        rootRb.constraints = RigidbodyConstraints.None;
        rootRb.isKinematic = false;
        rootRb.useGravity = true;



        rootRb.detectCollisions = true;
        if (cap != null)
            cap.enabled = true;



        rootRb.AddTorque(Random.insideUnitSphere * 7f + Vector3.forward * 2f,

            ForceMode.VelocityChange);


    }



}