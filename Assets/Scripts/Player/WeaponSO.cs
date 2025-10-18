using UnityEngine;

[CreateAssetMenu(fileName = "WeaponSO", menuName = "Scriptable Objects/WeaponSO")]
public class WeaponSO : ScriptableObject
{
    public GameObject weaponPrefab;
    public int Damage = 1;
    public float FireRate = .5f;
    public GameObject HitVFXPrefab;

    [Header("Gun Settings")]
    public bool isAutomatic = false;
    public bool CanZoom = false;
    public float ZoomAmount = 10f;
    public float ZoomRotationSpeed = .3f;
    public int MagazineSize = 12;


    [Header("Melee Settings")]
    public bool isMelee = false;   // kalau true berarti melee weapon
    public float meleeRange = 2f;  // jarak depan player
    public float meleeRadius = 1f; // radius hitbox


}
