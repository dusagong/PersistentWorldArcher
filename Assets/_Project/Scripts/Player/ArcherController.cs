using System;
using System.Collections;
using PersistentWorld.Combat;
using PersistentWorld.Core;
using PersistentWorld.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentWorld.Player
{
    // Original name/GUID retained for existing scenes and team references.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class ArcherController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4;
        [SerializeField] private float backstepDistance = 2;
        [SerializeField] private float backstepDuration = .16f;
        [SerializeField] private float backstepCooldown = 2;
        private Rigidbody2D body;
        private Health health;
        private SkillLoadout loadout;
        private PlayerSkills skills;
        private Vector2 moveInput;
        private Vector2 aimDirection = Vector2.right;
        private float backstepReadyAt;
        private float diedAt;
        private bool deathHandled;
        private bool waitForBoss;
        private bool isDashing;
        private Collider2D[] colliders;
        private bool[] colliderStates;
        private SpriteRenderer visual;
        private Sprite archerSprite;
        private Sprite placeholder;
        private CharacterClass visualClass = (CharacterClass)(-1);
        public Health Health => health;
        public SkillLoadout Loadout => loadout;
        public PlayerSkills Skills => skills;
        public Vector2 AimDirection => aimDirection;
        public bool IsDashing => isDashing;
        public bool MenuOpen { get; private set; }
        public bool AcceptLocalInput { get; set; } = true;
        public void SetAim(Vector2 direction) { if (direction.sqrMagnitude > .001f) aimDirection = direction.normalized; }
        public string NearbyPrompt { get; private set; }
        public float BackstepRemaining => Mathf.Max(0, backstepReadyAt - Time.time);
        public float DoubleShotRemaining => skills.Cooldown(SkillId.Repulse);
        public float ArrowRainRemaining => skills.Cooldown(SkillId.ArrowRain);
        public float PowerShotRemaining => skills.Cooldown(SkillId.Snipe);
        public float FocusRemaining => skills.SpecialRemaining;
        public bool IsFocused => loadout.Class == CharacterClass.Archer && skills.SpecialActive;
        public bool WaitingForBoss => health.IsDead && waitForBoss && BossEncounter.Active;
        public float ReturnRemaining => health.IsDead && !waitForBoss ? Mathf.Max(0, 10 - (Time.time - diedAt)) : 0;
        public event Action BackstepStarted;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); health = GetComponent<Health>();
            health.KeepOnDeath();
            loadout = GetComponent<SkillLoadout>() ?? gameObject.AddComponent<SkillLoadout>();
            if (GetComponent<CombatStatus>() == null) gameObject.AddComponent<CombatStatus>();
            skills = GetComponent<PlayerSkills>() ?? gameObject.AddComponent<PlayerSkills>();
            visual = GetComponent<SpriteRenderer>();
            colliders = GetComponentsInChildren<Collider2D>();
            colliderStates = new bool[colliders.Length];
            health.Died += OnDeath;
        }

        private void Start()
        {
            archerSprite = visual != null ? visual.sprite : null;
            UpdateClassVisual();
        }

        private void OnDestroy() { if (health != null) health.Died -= OnDeath; }
        private void OnDisable()
        {
            if (skills != null) skills.Cancel();
            StopAllCoroutines(); isDashing = false;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            if (health.IsDead)
            {
                if (!deathHandled) OnDeath(health);
                if (waitForBoss ? !BossEncounter.Active : Time.time - diedAt >= 10) ReturnToTown();
                return;
            }
            if (deathHandled) OnRevived();
            UpdateClassVisual();
            if (!AcceptLocalInput) { moveInput = Vector2.zero; return; }
            if (Input.GetKeyDown(KeyCode.Escape)) SetMenuOpen(!MenuOpen);
            if (MenuOpen) { moveInput = Vector2.zero; return; }
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            Camera camera = Camera.main;
            Vector2 mouse = camera != null ? (Vector2)camera.ScreenToWorldPoint(Input.mousePosition) : (Vector2)transform.position + aimDirection;
            Vector2 delta = mouse - (Vector2)transform.position;
            if (delta.sqrMagnitude > .01f) aimDirection = delta.normalized;
            skills.UpdateTarget(mouse);
            IInteractable nearby = FindNearestInteractable();
            NearbyPrompt = nearby == null ? "" : "F - " + nearby.Prompt;
            if (Input.GetKeyDown(KeyCode.F) && !skills.IsHolding) nearby?.Interact(gameObject);
            if (Input.GetKeyUp(KeyCode.Q)) skills.ReleaseSkill(0);
            if (Input.GetKeyUp(KeyCode.E)) skills.ReleaseSkill(1);
            if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) skills.ReleaseSkill(2);
            if (isDashing) return;
            if (Input.GetKeyDown(KeyCode.Space)) TryBackstep();
            else if (Input.GetKeyDown(KeyCode.Q)) skills.BeginSkill(0, mouse);
            else if (Input.GetKeyDown(KeyCode.E)) skills.BeginSkill(1, mouse);
            else if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) skills.BeginSkill(2, mouse);
            else if (Input.GetMouseButtonDown(0)) skills.TrySpecial();
            else if (Input.GetMouseButton(1)) skills.TryBasicAttack();
        }

        private void FixedUpdate()
        {
            if (health.IsDead || MenuOpen || skills.MovementLocked) { body.linearVelocity = Vector2.zero; return; }
            if (!isDashing && !GetComponent<CombatStatus>().IsDisplaced) body.linearVelocity = moveInput * moveSpeed;
        }

        public void SetMenuOpen(bool open)
        {
            if (health.IsDead) return;
            MenuOpen = open;
            if (open) { skills.Cancel(); StopAllCoroutines(); isDashing = false; body.linearVelocity = Vector2.zero; }
        }

        public bool TryBasicAttack() => skills.TryBasicAttack();
        public bool TryDoubleShot() => TryEquipped(SkillId.Repulse, false, transform.position);
        public bool TryArrowRain(Vector2 target) => TryEquipped(SkillId.ArrowRain, true, target);
        public bool TryPowerShot() => TryEquipped(SkillId.Snipe, true, transform.position);
        private bool TryEquipped(SkillId id, bool release, Vector2 target)
        {
            int slot = Array.IndexOf(loadout.Slots, id);
            if (slot < 0 || !skills.BeginSkill(slot, target)) return false;
            return !release || skills.ReleaseSkill(slot);
        }

        public bool TryBackstep()
        {
            if (!isActiveAndEnabled || health.IsDead || MenuOpen || isDashing || skills.IsHolding || skills.MovementLocked || Time.time < backstepReadyAt) return false;
            StartCoroutine(Backstep()); return true;
        }

        private IEnumerator Backstep()
        {
            skills.Cancel();
            isDashing = true; backstepReadyAt = Time.time + backstepCooldown;
            BackstepStarted?.Invoke();
            Vector2 direction = moveInput.sqrMagnitude > .01f ? moveInput : -aimDirection;
            float elapsed = 0;
            while (elapsed < backstepDuration && !health.IsDead)
            {
                body.linearVelocity = direction * backstepDistance / backstepDuration;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            body.linearVelocity = Vector2.zero; isDashing = false;
        }

        private void OnDeath(Health dead)
        {
            if (deathHandled) return;
            deathHandled = true; diedAt = Time.time; waitForBoss = BossEncounter.Active;
            MenuOpen = false; NearbyPrompt = ""; moveInput = Vector2.zero;
            StopAllCoroutines(); isDashing = false; skills.OnDeath();
            body.linearVelocity = Vector2.zero; body.simulated = false;
            for (int i = 0; i < colliders.Length; i++) { colliderStates[i] = colliders[i].enabled; colliders[i].enabled = false; }
            ArcherSpriteAnimator animator = GetComponent<ArcherSpriteAnimator>();
            if (animator != null) animator.enabled = false;
            if (visual != null) visual.color = new Color(.45f, .45f, .45f, .6f);
            transform.rotation = Quaternion.Euler(0, 0, -90);
        }

        private void OnRevived()
        {
            deathHandled = waitForBoss = false; body.simulated = true;
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderStates[i];
            transform.rotation = Quaternion.identity;
            visualClass = (CharacterClass)(-1); UpdateClassVisual();
        }

        private void ReturnToTown()
        {
            DialogueState.Clear();
            SceneManager.LoadScene("Town");
        }

        private void UpdateClassVisual()
        {
            if (visual == null || visualClass == loadout.Class) return;
            visualClass = loadout.Class;
            ArcherSpriteAnimator animator = GetComponent<ArcherSpriteAnimator>();
            if (animator != null) animator.enabled = visualClass == CharacterClass.Archer;
            transform.localScale = Vector3.one;
            if (visualClass == CharacterClass.Archer)
            {
                if (archerSprite != null) visual.sprite = archerSprite;
                visual.color = Color.white;
            }
            else
            {
                Sprite custom = Resources.Load<Sprite>("Characters/" + visualClass);
                if (custom != null) { visual.sprite = custom; visual.color = Color.white; }
                else
                {
                    if (placeholder == null)
                    {
                        Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply();
                        placeholder = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, 0), 1.5f);
                    }
                    visual.sprite = placeholder;
                    visual.color = visualClass == CharacterClass.Warrior ? new Color(1, .55f, .2f) : new Color(.35f, 1, .8f);
                }
            }
        }

        private IInteractable FindNearestInteractable()
        {
            IInteractable nearest = null; float nearestDistance = float.MaxValue;
            foreach (Collider2D candidate in Physics2D.OverlapCircleAll(transform.position, 1.2f))
                foreach (MonoBehaviour behaviour in candidate.GetComponentsInParent<MonoBehaviour>())
                    if (behaviour is IInteractable interactable)
                    {
                        float distance = Vector2.SqrMagnitude(candidate.transform.position - transform.position);
                        if (distance < nearestDistance) { nearest = interactable; nearestDistance = distance; }
                    }
            return nearest;
        }
    }
}
