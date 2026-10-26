using System;
using System.Collections;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using UnityEngine;

namespace PersistentWorld.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class ArcherController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float backstepDistance = 2f;
        [SerializeField] private float backstepDuration = 0.16f;
        [SerializeField] private float backstepCooldown = 2f;

        [Header("Combat")]
        [SerializeField] private int attackDamage = 10;
        [SerializeField] private float attackInterval = 0.45f;
        [SerializeField] private float doubleShotCooldown = 4f;
        [SerializeField] private float focusDuration = 5f;
        [SerializeField] private float focusCooldown = 10f;

        private Rigidbody2D body;
        private Health health;
        private Vector2 moveInput;
        private Vector2 aimDirection = Vector2.right;
        private float attackReadyAt;
        private float doubleShotReadyAt;
        private float backstepReadyAt;
        private float focusReadyAt;
        private float focusEndsAt;
        private bool isDashing;

        public Health Health => health;
        public float DoubleShotRemaining => Mathf.Max(0f, doubleShotReadyAt - Time.time);
        public float BackstepRemaining => Mathf.Max(0f, backstepReadyAt - Time.time);
        public float FocusRemaining => Mathf.Max(0f, focusReadyAt - Time.time);
        public bool IsFocused => Time.time < focusEndsAt;
        public Vector2 AimDirection => aimDirection;
        public event Action BackstepStarted;
        public string NearbyPrompt { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (health.IsDead)
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            UpdateAim();
            UpdateInteraction();

            if (Input.GetKeyDown(KeyCode.F))
            {
                TryInteract();
            }

            if (isDashing || IsPointerOverUi())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.E) && Time.time >= backstepReadyAt)
            {
                TryBackstep();
            }
            else if (Input.GetMouseButtonDown(1) && Time.time >= focusReadyAt)
            {
                ActivateFocus();
            }
            else if (Input.GetKeyDown(KeyCode.Q) && Time.time >= doubleShotReadyAt)
            {
                StartCoroutine(DoubleShot());
            }
            else if (Input.GetMouseButton(0) && Time.time >= attackReadyAt)
            {
                FireArrow();
            }
        }

        private void FixedUpdate()
        {
            if (!isDashing && !health.IsDead)
            {
                body.linearVelocity = moveInput * moveSpeed;
            }
        }

        private void UpdateAim()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 candidate = mouseWorld - (Vector2)transform.position;
            if (candidate.sqrMagnitude > 0.01f)
            {
                aimDirection = candidate.normalized;
            }
        }

        private void FireArrow(float angleOffset = 0f)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, angleOffset) * aimDirection;
            int damage = IsFocused ? Mathf.RoundToInt(attackDamage * 1.5f) : attackDamage;
            Color color = IsFocused ? new Color(1f, 0.8f, 0.15f) : new Color(0.9f, 1f, 0.3f);
            Projectile.Spawn((Vector2)transform.position + direction * 0.5f, direction, damage, Faction.Player, gameObject, color);
            attackReadyAt = Time.time + (IsFocused ? attackInterval * 0.6f : attackInterval);
        }

        private IEnumerator DoubleShot()
        {
            doubleShotReadyAt = Time.time + doubleShotCooldown;
            FireArrow(-3f);
            yield return new WaitForSeconds(0.12f);
            if (!health.IsDead)
            {
                FireArrow(3f);
            }
        }

        public bool TryBackstep()
        {
            if (!isActiveAndEnabled || health.IsDead || isDashing || Time.time < backstepReadyAt) return false;
            StartCoroutine(Backstep());
            return true;
        }

        private IEnumerator Backstep()
        {
            isDashing = true;
            BackstepStarted?.Invoke();
            backstepReadyAt = Time.time + backstepCooldown;
            Vector2 dashDirection = -aimDirection;
            float speed = backstepDistance / backstepDuration;
            float elapsed = 0f;

            while (elapsed < backstepDuration && !health.IsDead)
            {
                body.linearVelocity = dashDirection * speed;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            body.linearVelocity = Vector2.zero;
            isDashing = false;
        }

        private void ActivateFocus()
        {
            focusEndsAt = Time.time + focusDuration;
            focusReadyAt = Time.time + focusCooldown;
        }

        private void UpdateInteraction()
        {
            IInteractable interactable = FindNearestInteractable();
            NearbyPrompt = interactable == null ? string.Empty : "F - " + interactable.Prompt;
        }

        private void TryInteract()
        {
            FindNearestInteractable()?.Interact(gameObject);
        }

        private IInteractable FindNearestInteractable()
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 1.2f);
            IInteractable nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (Collider2D candidate in colliders)
            {
                MonoBehaviour[] behaviours = candidate.GetComponentsInParent<MonoBehaviour>();
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is not IInteractable interactable)
                    {
                        continue;
                    }

                    float distance = Vector2.SqrMagnitude(candidate.transform.position - transform.position);
                    if (distance < nearestDistance)
                    {
                        nearest = interactable;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        private static bool IsPointerOverUi()
        {
            return false;
        }
    }
}

