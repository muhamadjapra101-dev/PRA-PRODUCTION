using System;
using System.Collections;
using UnityEngine;
using StarterAssets;
using Cinemachine;
using TMPro;

public class ActiveWeapon : MonoBehaviour
{
    [Header("Weapon setup")]
    [SerializeField] WeaponSO startingWeapon;
    [SerializeField] WeaponSO[] weaponInventory; // assign weapons in inspector (index 0 -> key 1)
    [Header("Cameras & UI")]
    [SerializeField] CinemachineVirtualCamera playerFollowCamera;
    [SerializeField] Camera weaponCamera;
    [SerializeField] GameObject zoomVignette;
    [SerializeField] TMP_Text ammoText;

    [Header("Fallback reload (used if WeaponSO.ReloadTime not set)")]
    [SerializeField] float fallbackReloadTime = 1.5f;

    // Runtime
    public event Action<float> OnReloadProgress; // value 0..1
    public event Action OnReloadComplete;

    WeaponSO currentWeaponSO;
    Animator animator;
    StarterAssetsInputs starterAssetsInputs;
    FirstPersonController firstPersonController;
    Weapon currentWeapon;

    const string SHOOT_STRING = "Shoot";

    float timeSinceLastShot = 0f;
    float defaultFOV;
    float defaultRotationSpeed;

    int currentAmmo;
    int currentWeaponIndex = 0;

    int[] ammoPerWeapon;          // magazine per weapon
    int[] reserveAmmoPerWeapon;   // reserve per weapon

    bool isReloading = false;
    Coroutine reloadCoroutine;

    void Awake()
    {
        starterAssetsInputs = GetComponentInParent<StarterAssetsInputs>();
        firstPersonController = GetComponentInParent<FirstPersonController>();
        animator = GetComponent<Animator>();
        defaultFOV = playerFollowCamera != null ? playerFollowCamera.m_Lens.FieldOfView : 60f;
        defaultRotationSpeed = firstPersonController != null ? firstPersonController.RotationSpeed : 1f;
    }

    void Start()
    {
        // Ensure inventory exists
        if (weaponInventory == null || weaponInventory.Length == 0)
        {
            if (startingWeapon != null)
                weaponInventory = new WeaponSO[] { startingWeapon };
            else
                weaponInventory = new WeaponSO[0];
        }

        // determine starting index (if startingWeapon in inventory)
        currentWeaponIndex = 0;
        if (startingWeapon != null && weaponInventory.Length > 0)
        {
            int idx = Array.IndexOf(weaponInventory, startingWeapon);
            if (idx >= 0) currentWeaponIndex = idx;
        }

        // init arrays
        ammoPerWeapon = new int[weaponInventory.Length];
        reserveAmmoPerWeapon = new int[weaponInventory.Length];

        for (int i = 0; i < weaponInventory.Length; i++)
        {
            var so = weaponInventory[i];
            ammoPerWeapon[i] = Mathf.Clamp(so != null ? so.MagazineSize : 0, 0, (so != null ? so.MagazineSize : 0));
            reserveAmmoPerWeapon[i] = so != null ? so.StartingReserveAmmo : 0;
        }

        // equip the weapon
        if (weaponInventory.Length > 0)
            SwitchWeaponByIndex(currentWeaponIndex);
        else
            UpdateAmmoUI();
    }

    void Update()
    {
        HandleShoot();
        HandleZoom();
        HandleWeaponSwitchInput();
        HandleReloadInput();
    }

    // PUBLIC: add reserve ammo to the currently selected weapon (e.g. pickup)
    public void AddReserveAmmo(int amount)
    {
        if (weaponInventory == null || weaponInventory.Length == 0) return;
        reserveAmmoPerWeapon[currentWeaponIndex] += amount;
        UpdateAmmoUI();
    }

    // PUBLIC: adjust magazine ammo of current weapon (used internally)
    public void AdjustAmmo(int amount)
    {
        if (weaponInventory == null || weaponInventory.Length == 0) return;

        ammoPerWeapon[currentWeaponIndex] = Mathf.Clamp(
            ammoPerWeapon[currentWeaponIndex] + amount,
            0,
            currentWeaponSO != null ? currentWeaponSO.MagazineSize : ammoPerWeapon[currentWeaponIndex]
        );

        currentAmmo = ammoPerWeapon[currentWeaponIndex];
        UpdateAmmoUI();
    }

    // Switch via WeaponSO for compatibility
    public void SwitchWeapon(WeaponSO weaponSO)
    {
        if (weaponInventory == null || weaponInventory.Length == 0 || weaponSO == null) return;
        int idx = Array.IndexOf(weaponInventory, weaponSO);
        if (idx >= 0) SwitchWeaponByIndex(idx);
    }

    // Core switching by index
    void SwitchWeaponByIndex(int newIndex)
    {
        if (weaponInventory == null || weaponInventory.Length == 0) return;
        if (newIndex < 0 || newIndex >= weaponInventory.Length) return;

        // if same index, ignore
        if (currentWeapon != null && newIndex == currentWeaponIndex) return;

        // stop reload if in progress
        if (isReloading)
        {
            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }
            isReloading = false;
            OnReloadProgress?.Invoke(0f);
        }

        // destroy old weapon GameObject
        if (currentWeapon != null)
        {
            Destroy(currentWeapon.gameObject);
            currentWeapon = null;
        }

        // instantiate new weapon prefab
        WeaponSO newSO = weaponInventory[newIndex];
        if (newSO != null && newSO.weaponPrefab != null)
        {
            Weapon newW = Instantiate(newSO.weaponPrefab, transform).GetComponent<Weapon>();
            currentWeapon = newW;
            currentWeaponSO = newSO;
            currentWeaponIndex = newIndex;
        }
        else
        {
            currentWeaponSO = newSO;
            currentWeaponIndex = newIndex;
            currentWeapon = null;
        }

        // restore ammo values for that weapon
        currentAmmo = ammoPerWeapon[currentWeaponIndex];
        UpdateAmmoUI();
    }

    void UpdateAmmoUI()
    {
        if (ammoText == null) return;

        if (weaponInventory == null || weaponInventory.Length == 0)
        {
            ammoText.text = "--";
            return;
        }

        int magazine = ammoPerWeapon[currentWeaponIndex];
        int reserve = reserveAmmoPerWeapon[currentWeaponIndex];
        ammoText.text = $"{magazine:D2} | {reserve:D2}";
    }

    void HandleShoot()
    {
        if (currentWeaponSO == null) return;

        timeSinceLastShot += Time.deltaTime;

        if (isReloading) return; // cannot shoot during reload

        if (!starterAssetsInputs.shoot) return;

        if (timeSinceLastShot >= currentWeaponSO.FireRate && currentAmmo > 0)
        {
            currentWeapon?.Shoot(currentWeaponSO);
            animator?.Play(SHOOT_STRING, 0, 0f);
            timeSinceLastShot = 0f;
            AdjustAmmo(-1);

            // if magazine empty after shooting and there's reserve, optionally auto reload
            if (ammoPerWeapon[currentWeaponIndex] == 0 && reserveAmmoPerWeapon[currentWeaponIndex] > 0)
            {
                // auto-start reload after short delay or immediately
                TryStartReload();
            }
        }

        if (!currentWeaponSO.isAutomatic)
        {
            starterAssetsInputs.ShootInput(false);
        }
    }

    void HandleZoom()
    {
        if (currentWeaponSO == null) return;
        if (!currentWeaponSO.CanZoom) return;

        if (starterAssetsInputs.zoom)
        {
            if (playerFollowCamera != null) playerFollowCamera.m_Lens.FieldOfView = currentWeaponSO.ZoomAmount;
            if (weaponCamera != null) weaponCamera.fieldOfView = currentWeaponSO.ZoomAmount;
            if (zoomVignette != null) zoomVignette.SetActive(true);
            firstPersonController?.ChangeRotationSpeed(currentWeaponSO.ZoomRotationSpeed);
        }
        else
        {
            if (playerFollowCamera != null) playerFollowCamera.m_Lens.FieldOfView = defaultFOV;
            if (weaponCamera != null) weaponCamera.fieldOfView = defaultFOV;
            if (zoomVignette != null) zoomVignette.SetActive(false);
            firstPersonController?.ChangeRotationSpeed(defaultRotationSpeed);
        }
    }

    void HandleWeaponSwitchInput()
    {
        if (weaponInventory == null || weaponInventory.Length == 0) return;

        // Number keys 1..9
        int maxKeys = Mathf.Min(weaponInventory.Length, 9);
        for (int i = 0; i < maxKeys; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;
            if (Input.GetKeyDown(key))
            {
                SwitchWeaponByIndex(i);
                return;
            }
        }

        // Mouse wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            int newIndex = currentWeaponIndex;
            if (scroll > 0f) newIndex = (currentWeaponIndex - 1 + weaponInventory.Length) % weaponInventory.Length;
            else newIndex = (currentWeaponIndex + 1) % weaponInventory.Length;

            SwitchWeaponByIndex(newIndex);
        }
    }

    void HandleReloadInput()
    {
        if (weaponInventory == null || weaponInventory.Length == 0) return;
        if (currentWeaponSO == null) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            TryStartReload();
        }
    }

    void TryStartReload()
    {
        if (isReloading) return;
        if (ammoPerWeapon[currentWeaponIndex] >= (currentWeaponSO != null ? currentWeaponSO.MagazineSize : 0)) return;
        if (reserveAmmoPerWeapon[currentWeaponIndex] <= 0) return;

        // start reload coroutine (stops previous if exists)
        if (reloadCoroutine != null) StopCoroutine(reloadCoroutine);
        reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;

        // get reload time from WeaponSO (fallback if <= 0)
        float reloadTime = (currentWeaponSO != null && currentWeaponSO.ReloadTime > 0f) ? currentWeaponSO.ReloadTime : fallbackReloadTime;

        // trigger animator (if name provided)
        if (animator != null && currentWeaponSO != null && !string.IsNullOrEmpty(currentWeaponSO.ReloadAnimationName))
        {
            // prefer Trigger if available on animator. Using SetTrigger is safer than Play (keeps transitions).
            try
            {
                animator.SetTrigger(currentWeaponSO.ReloadAnimationName);
            }
            catch
            {
                // fallback to Play if trigger not present
                animator.Play(currentWeaponSO.ReloadAnimationName, 0, 0f);
            }
        }

        float timer = 0f;
        while (timer < reloadTime)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / reloadTime);
            OnReloadProgress?.Invoke(progress);
            yield return null;
        }

        // Calculate transfer from reserve to magazine
        int needed = (currentWeaponSO != null ? currentWeaponSO.MagazineSize : ammoPerWeapon[currentWeaponIndex]) - ammoPerWeapon[currentWeaponIndex];
        int available = reserveAmmoPerWeapon[currentWeaponIndex];
        int transfer = Mathf.Min(needed, available);

        ammoPerWeapon[currentWeaponIndex] += transfer;
        reserveAmmoPerWeapon[currentWeaponIndex] -= transfer;
        currentAmmo = ammoPerWeapon[currentWeaponIndex];

        // update UI and notify complete
        UpdateAmmoUI();
        OnReloadProgress?.Invoke(1f);
        OnReloadComplete?.Invoke();

        // clear reload state
        isReloading = false;
        reloadCoroutine = null;
    }
}