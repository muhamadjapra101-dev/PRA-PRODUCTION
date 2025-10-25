using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		public float MoveSpeed = 4.0f;
		public float SprintSpeed = 6.0f;
		public float RotationSpeed = 1.0f;
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		public float JumpHeight = 1.2f;
		public float Gravity = -15.0f;

		[Space(10)]
		public float JumpTimeout = 0.1f;
		public float FallTimeout = 0.15f;

		[Space(10)]
		public float CrouchHeight = 1.0f;
		public float NormalHeight = 2.0f;
		public float CrouchSpeed = 2.0f;
		public float SlideSpeed = 8.0f;
		public float SlideDuration = 0.5f;
		public float DashSpeed = 14.0f;
		public float DashDuration = 0.2f;
		public float DashCooldown = 1f;

		[Header("Height/Camera/Slide Visuals")]
		[Tooltip("How much above the bottom the camera should sit (tweak for your sprite pivot)")]
		public float CameraHeightOffset = 0.9f;
		[Tooltip("Smooth time for height transitions (smaller = faster)")]
		public float HeightSmoothTime = 0.06f;
		[Tooltip("Smooth time for camera Y transitions")]
		public float CameraHeightSmoothTime = 0.06f;
		[Tooltip("Tilt angle (degrees) applied to camera pitch during slide (positive = look down)")]
		public float SlideTiltAngle = 12f;
		[Tooltip("Smooth time for tilt changes")]
		public float SlideTiltSmoothTime = 0.08f;
		[Tooltip("Layers considered as environment/ceiling when checking if we can stand")]
		public LayerMask EnvironmentLayers;

		[Header("Player Grounded")]
		public bool Grounded = true;
		public float GroundedOffset = -0.14f;
		public float GroundedRadius = 0.5f;
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		public GameObject CinemachineCameraTarget;
		public float TopClamp = 90.0f;
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// dash,crouch,slide
		private float _slideTimer = 0f;
		private float _dashTimer = 0f;
		private float _dashCooldownTimer = 0f;
		private bool _isCrouching = false;
		private bool _isSliding = false;
		private bool _isDashing = false;

		// height smoothing
		private float _heightVelocity = 0f;
		private float _targetHeight;

		// camera smoothing
		private float _cameraHeightVelocity = 0f;
		private float _currentCameraLocalY = 0f;

		// tilt smoothing
		private float _currentTilt = 0f;
		private float _tiltVelocity = 0f;
		private float _targetTilt = 0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;


#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
			}
		}

		private void Awake()
		{
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			// init heights and camera Y
			_targetHeight = NormalHeight;
			_controller.height = NormalHeight;
			_controller.center = new Vector3(_controller.center.x, NormalHeight / 2f, _controller.center.z);

			if (CinemachineCameraTarget != null)
			{
				_currentCameraLocalY = CinemachineCameraTarget.transform.localPosition.y;
			}
		}

		private void Update()
		{
			JumpAndGravity();
			GroundedCheck();
			HandleDash();
			HandleSlide();
			HandleCrouch(); // sets _targetHeight
			ApplyHeightSmooth(); // apply smooth height+center changes
			ApplyCameraHeightSmooth();
			ApplyTiltSmooth();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		public void ChangeRotationSpeed(float amount)
		{
			RotationSpeed = amount;
		}

		private void GroundedCheck()
		{
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			if (_input.look.sqrMagnitude >= _threshold)
			{
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// combine pitch with tilt. Tilt is added to pitch (so slide -> look slightly down)
				if (CinemachineCameraTarget != null)
				{
					Quaternion targetRot = Quaternion.Euler(_cinemachineTargetPitch + _currentTilt, 0.0f, 0.0f);
					CinemachineCameraTarget.transform.localRotation = targetRot;
				}

				transform.Rotate(Vector3.up * _rotationVelocity);
			}
			else
			{
				// Even if no look input, we must still apply tilt combination (e.g., when sliding)
				if (CinemachineCameraTarget != null)
				{
					Quaternion targetRot = Quaternion.Euler(_cinemachineTargetPitch + _currentTilt, 0.0f, 0.0f);
					CinemachineCameraTarget.transform.localRotation = targetRot;
				}
			}
		}

		private void Move()
		{
			float targetSpeed = _isDashing ? DashSpeed :
								_isSliding ? SlideSpeed :
								_isCrouching ? CrouchSpeed :
								_input.sprint ? SprintSpeed : MoveSpeed;

			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			if (_input.move != Vector2.zero)
			{
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				_fallTimeoutDelta = FallTimeout;

				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				_jumpTimeoutDelta = JumpTimeout;
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}
				_input.jump = false;
			}

			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.35f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}

		private void HandleDash()
		{
			if (_dashCooldownTimer > 0f)
				_dashCooldownTimer -= Time.deltaTime;

			if (_input.dash && !_isDashing && _dashCooldownTimer <= 0f && _input.move != Vector2.zero)
			{
				_isDashing = true;
				_dashTimer = DashDuration;
				_dashCooldownTimer = DashCooldown;
			}

			if (_isDashing)
			{
				Vector3 dashDir = (transform.right * _input.move.x + transform.forward * _input.move.y).normalized;
				_controller.Move(dashDir * DashSpeed * Time.deltaTime);
				_dashTimer -= Time.deltaTime;
				if (_dashTimer <= 0f)
					_isDashing = false;
			}
		}

		private void HandleCrouch()
		{
			if (_input.crouch && Grounded && !_isSliding && !_isDashing)
			{
				if (!_isCrouching)
				{
					_isCrouching = true;
				}
				_targetHeight = CrouchHeight;
			}
			else
			{
				if (_isCrouching && !_input.crouch)
				{
					if (CanStand())
					{
						_isCrouching = false;
						_targetHeight = NormalHeight;
					}
					else
					{
						_targetHeight = CrouchHeight;
					}
				}
			}
		}

		private void HandleSlide()
		{
			if (_input.slide && Grounded && _input.sprint && _input.move != Vector2.zero && !_isSliding && !_isDashing)
			{
				_isSliding = true;
				_slideTimer = SlideDuration;
				_targetHeight = CrouchHeight;
			}

			if (_isSliding)
			{
				// Visual tilt target when sliding
				_targetTilt = -SlideTiltAngle;

				Vector3 slideDir = transform.forward;
				_controller.Move(slideDir * SlideSpeed * Time.deltaTime);
				_slideTimer -= Time.deltaTime;
				if (_slideTimer <= 0f || !_input.slide || _input.move == Vector2.zero)
				{
					_isSliding = false;
					_targetTilt = 0f;
					_targetHeight = _input.crouch ? CrouchHeight : NormalHeight;
					_isCrouching = _input.crouch;
				}
			}
			else
			{
				_targetTilt = 0f; // no tilt by default
			}
		}

		private void ApplyHeightSmooth()
		{
			if (_controller == null) return;

			float newHeight = Mathf.SmoothDamp(_controller.height, _targetHeight, ref _heightVelocity, HeightSmoothTime);
			_controller.height = newHeight;

			// Keep bottom aligned to transform.position (so capsule "shrinks downward")
			_controller.center = new Vector3(_controller.center.x, newHeight / 2f, _controller.center.z);
		}

		private void ApplyCameraHeightSmooth()
		{
			if (CinemachineCameraTarget == null) return;

			// desired camera local Y (distance above transform bottom). Tweak CameraHeightOffset in inspector.
			float desiredCameraY = Mathf.Max(0.1f, _controller.height - CameraHeightOffset);

			_currentCameraLocalY = Mathf.SmoothDamp(CinemachineCameraTarget.transform.localPosition.y, desiredCameraY, ref _cameraHeightVelocity, CameraHeightSmoothTime);

			Vector3 lp = CinemachineCameraTarget.transform.localPosition;
			lp.y = _currentCameraLocalY;
			CinemachineCameraTarget.transform.localPosition = lp;
		}

		private void ApplyTiltSmooth()
		{
			_currentTilt = Mathf.SmoothDamp(_currentTilt, _targetTilt, ref _tiltVelocity, SlideTiltSmoothTime);
			// Tilt is applied in CameraRotation when setting localRotation (pitch + tilt)
			// So nothing else to do here.
		}

		private bool CanStand()
		{
			float radius = Mathf.Max(0.01f, _controller.radius * 0.9f);
			Vector3 bottom = transform.position + Vector3.up * radius;
			Vector3 top = transform.position + Vector3.up * (NormalHeight - radius);
			bool hit = Physics.CheckCapsule(bottom, top, radius, EnvironmentLayers, QueryTriggerInteraction.Ignore);
			return !hit;
		}
	}
}