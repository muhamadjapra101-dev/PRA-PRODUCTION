using UnityEngine;

public class MeleeWeapon : MonoBehaviour
{
    [Header("Melee Settings")]
    public float meleeRange = 2f;      // Jarak dari player ke pusat hitbox
    public float meleeRadius = 1f;     // Radius area hitbox
    public int damage = 10;            // Damage yang diberikan
    public LayerMask enemyLayers;      // Layer musuh (set di Inspector)
    public GameObject hitVFXPrefab;    // (Opsional) efek visual saat kena musuh

    /// <summary>
    /// Panggil fungsi ini ketika serangan melee di-trigger (misal: animasi, input, dsb)
    /// </summary>
    public void Swing()
    {
        // Titik pusat sphere di depan kamera/player
        Vector3 center = Camera.main.transform.position + Camera.main.transform.forward * meleeRange;

        // Cek collider musuh dalam radius
        Collider[] hits = Physics.OverlapSphere(center, meleeRadius, enemyLayers);

        foreach (Collider hit in hits)
        {
            EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                if (hitVFXPrefab != null)
                    Instantiate(hitVFXPrefab, hit.transform.position, Quaternion.identity);
            }
        }

        Debug.Log("Melee swing! Kena: " + hits.Length + " musuh.");
    }

    // Opsional: Visualisasi area hitbox di editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = Camera.main != null
            ? Camera.main.transform.position + Camera.main.transform.forward * meleeRange
            : transform.position + transform.forward * meleeRange;

        Gizmos.DrawWireSphere(center, meleeRadius);
    }
    void Update()
{
    if (Input.GetKeyDown(KeyCode.T)) // tombol T buat tes
    {
        Swing();
    }
}
}

//{
// [SerializeField] LayerMask hitLayers; // layer musuh/target

// public void Swing(WeaponSO weaponSO)
//{
       // if (!weaponSO.isMelee) return;

        // posisi sphere ada di depan kamera
       // Vector3 center = Camera.main.transform.position + Camera.main.transform.forward * weaponSO.meleeRange;

        // cari musuh dalam radius
       // Collider[] hits = Physics.OverlapSphere(center, weaponSO.meleeRadius, hitLayers);

        //foreach (Collider hit in hits)
       // {
         //   EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
           // if (enemy != null)
           // {
               // enemy.TakeDamage(weaponSO.Damage);
              //  if (weaponSO.HitVFXPrefab)
                 //   Instantiate(weaponSO.HitVFXPrefab, hit.transform.position, Quaternion.identity);
          //  }
     //   }

      //  Debug.Log("Melee swing triggered!");
   // }
//}
