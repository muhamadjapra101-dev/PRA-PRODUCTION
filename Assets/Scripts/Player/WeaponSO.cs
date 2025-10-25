using UnityEngine;

[CreateAssetMenu(fileName = "WeaponSO", menuName = "Scriptable Objects/WeaponSO")]
public class WeaponSO : ScriptableObject
{public string WeaponName;
    public GameObject weaponPrefab;
    public GameObject HitVFXPrefab;
    public int Damage = 25;

    [Header("Firing")]
    public float FireRate = 0.12f;
    public int MagazineSize = 30;
    public bool isAutomatic = true;

    [Header("Zoom")]
    public bool CanZoom = false;
    public float ZoomAmount = 40f;
    public float ZoomRotationSpeed = 4f;

    [Header("Reload")]
    public float ReloadTime = 1.5f;           // durasi reload untuk weapon ini
    public string ReloadAnimationName = "Reload"; // nama animasi reload di Animator (kosong = no anim)
    public int StartingReserveAmmo = 90;    
   
}
