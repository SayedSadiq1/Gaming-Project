using UnityEngine;
using Unity.FPS.Game;

public class EnemyBullet : MonoBehaviour
{
    public float speed = 40f;
    public float damage = 10f;
    public float lifeTime = 3f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        Damageable damageable = other.GetComponent<Damageable>();

        if (damageable == null)
        {
            damageable = other.GetComponentInParent<Damageable>();
        }

        if (damageable != null)
        {
            damageable.InflictDamage(damage, false, gameObject);
        }

        Destroy(gameObject);
    }
}