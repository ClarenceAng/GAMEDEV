using System;
using UnityEngine;


public class VfxProjectile : MonoBehaviour
{
    public Vector3 direction = Vector3.forward;
    public float speed = 14f, lifetime = 1.6f, groundY = 0f, hitRadius = .3f;
    public Action<Vector3> onImpact;
    public ParticleSystem[] clearOnImpact;
    bool done;

    void Update()
    {
        if (done) return;
        float step = speed * Time.deltaTime;
        Vector3 p = transform.position;
        RaycastHit hit;
        if (Physics.SphereCast(p, hitRadius, direction, out hit, step + hitRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            Impact(hit.point);
            return;
        }
        transform.position = p + direction * step;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f || transform.position.y <= groundY) Impact(transform.position);
    }

    void Impact(Vector3 p)
    {
        done = true;
        if (onImpact != null) onImpact(p);
        if (clearOnImpact != null) foreach (var ps in clearOnImpact) if (ps) ps.Clear();
        foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        foreach (var l in GetComponentsInChildren<VfxLight>()) Destroy(l.gameObject);
        Destroy(gameObject, 3f);
    }
}
