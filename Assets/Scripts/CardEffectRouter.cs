using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// Decides what to actually DO when a card is recognized, based on its
    /// EffectCategory rather than its specific name. This is what lets you
    /// add card #11 by just adding a CardDatabase entry, not new code here.
    /// Card-specific *resolution* (which monster to destroy, etc.) does
    /// branch on card.id in a few places below — that's unavoidable once
    /// effects actually do different things, but it stays isolated to the
    /// method for that effect category rather than spreading everywhere.
    ///
    /// Ownership: every method below that needs to know "whose card is
    /// this" derives it from the zoneName via FieldZoneManager.GetZoneOwner
    /// rather than tracking it separately — the physical zone a card sits
    /// in IS who controls it.
    ///
    /// Summon legality: Normal Summon checks TurnManager.CanNormalSummon
    /// (your Main Phase, once per turn) and, for level 5+ monsters, prompts
    /// for the correct number of tributes before the monster actually joins
    /// MonsterFieldState. See PlayNormalSummon / RequestTributes below.
    /// </summary>
    public class CardEffectRouter : MonoBehaviour
    {
        [Tooltip("Prefab shown briefly while a category-specific handler isn't built yet")]
        public GameObject placeholderEffectPrefab;

        [Header("Normal Summon FX (Step 5)")]
        public ParticleSystem summonParticlesPrefab;
        public AudioClip summonSound;
        [Range(0f, 1f)] public float summonVolume = 0.8f;

        [Header("Flip Effect FX (Step 6)")]
        public ParticleSystem flipEffectParticlesPrefab;
        public AudioClip flipEffectSound;

        [Header("Trap FX (Step 8)")]
        public ParticleSystem trapTriggerParticlesPrefab;
        public AudioClip trapTriggerSound;

        [Header("Destroy FX (used by flip/trap effects)")]
        public ParticleSystem destroyParticlesPrefab;
        public AudioClip destroySound;

        [Header("UI Hooks")]
        [Tooltip("Optional: pops up automatically when a trap is set face-down")]
        public TrapIdentityPromptUI trapIdentityPrompt;
        [Tooltip("Optional: pops up automatically when a spell needs a target")]
        public TargetSelectionUI targetSelectionUI;

        private AudioSource audioSource;

        // Tracks armed traps by the zone they were set in.
        private readonly Dictionary<string, (CardData card, Transform t)> armedTraps =
            new Dictionary<string, (CardData, Transform)>();

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        private void OnEnable()
        {
            GameEvents.OnAttackDeclared += HandleAttackDeclared;
        }

        private void OnDisable()
        {
            GameEvents.OnAttackDeclared -= HandleAttackDeclared;
        }

        private PlayerSide OwnerOf(string zoneName) =>
            FieldZoneManager.Instance != null ? FieldZoneManager.Instance.GetZoneOwner(zoneName) : PlayerSide.Player;

        public void HandleRecognition(CardData card, Transform cardTransform, ZoneType zone, string zoneName, bool wasFlip)
        {
            if (card.effect == EffectCategory.NormalSummon && zone != ZoneType.Monster)
            {
                Debug.Log($"[CardEffectRouter] {card.displayName} seen but not in a Monster zone yet — waiting.");
                return;
            }

            switch (card.effect)
            {
                case EffectCategory.NormalSummon:
                    PlayNormalSummon(card, cardTransform, zoneName);
                    break;

                case EffectCategory.FlipEffect:
                    if (wasFlip)
                        PlayFlipEffect(card, cardTransform, zoneName);
                    else
                        Debug.Log($"[CardEffectRouter] {card.displayName} placed face-up directly — no flip trigger.");
                    break;

                case EffectCategory.SpellTarget:
                    PlaySpellTarget(card, cardTransform, zoneName);
                    break;

                case EffectCategory.SpellSimple:
                    PlaySpellSimple(card, cardTransform, zoneName);
                    break;

                case EffectCategory.TrapSetTrigger:
                    Debug.Log($"[CardEffectRouter] {card.displayName} recognized face-up (not via set flow).");
                    break;
            }
        }

        public void HandleFaceDownSet(ZoneType zone, string zoneName)
        {
            if (zone != ZoneType.SpellTrap) return;

            Debug.Log($"[CardEffectRouter] Something set face-down in {zoneName} — could be a trap.");

            if (trapIdentityPrompt != null)
                trapIdentityPrompt.Show(zoneName, transform);
            else
                Debug.Log("[CardEffectRouter] No trapIdentityPrompt assigned — call ArmKnownTrap() manually for now.");
        }

        public void ArmKnownTrap(string cardId, string zoneName, Transform t)
        {
            var card = CardDatabase.Get(cardId);
            if (card == null || card.effect != EffectCategory.TrapSetTrigger)
            {
                Debug.LogWarning($"[CardEffectRouter] '{cardId}' is not a valid trap card.");
                return;
            }

            armedTraps[zoneName] = (card, t);
            Debug.Log($"[CardEffectRouter] Trap armed: {card.displayName} in {zoneName} ({OwnerOf(zoneName)}'s).");
        }

        public void HandleCardLost(string cardId)
        {
            // Hook for cleanup: stop looping sounds, hide UI tied to this card, etc.
        }

        // --- Step 5 / Step 13: summon legality (once per turn, your Main Phase)
        // and tribute cost by level, then the actual FX + board placement ---
        private void PlayNormalSummon(CardData card, Transform t, string zoneName)
        {
            var owner = OwnerOf(zoneName);

            if (TurnManager.Instance != null && !TurnManager.Instance.CanNormalSummon(owner))
            {
                Debug.Log($"[CardEffectRouter] {card.displayName} can't be Normal Summoned right now " +
                          $"— not {owner}'s Main Phase, or their one Normal Summon this turn is already used.");
                return;
            }

            int tributesRequired = CardDatabase.TributesRequired(card.level);
            if (tributesRequired == 0)
            {
                CompleteNormalSummon(card, t, zoneName, owner);
                return;
            }

            var availableTributes = MonsterFieldState.Instance.GetAllMonsters(owner)
                .Where(m => m.zoneName != zoneName)
                .Select(m => m.cardId)
                .ToArray();

            if (availableTributes.Length < tributesRequired)
            {
                Debug.Log($"[CardEffectRouter] {card.displayName} (level {card.level}) needs {tributesRequired} " +
                          $"tribute(s), but {owner} only has {availableTributes.Length} other monster(s) on the field.");
                return;
            }

            Debug.Log($"[CardEffectRouter] {card.displayName} requires {tributesRequired} tribute(s) — requesting selection.");
            RequestTributes(card, t, zoneName, owner, tributesRequired, new List<string>());
        }

        private void RequestTributes(CardData card, Transform t, string zoneName, PlayerSide owner,
            int tributesRemaining, List<string> alreadyChosenZones)
        {
            var candidates = MonsterFieldState.Instance.GetAllMonsters(owner)
                .Where(m => m.zoneName != zoneName && !alreadyChosenZones.Contains(m.zoneName))
                .ToArray();

            var zoneNames = candidates.Select(c => c.zoneName).ToArray();
            var labels = candidates.Select(c => $"{CardDatabase.Get(c.cardId)?.displayName} ({c.zoneName})").ToArray();

            TargetSelectionManager.Instance.RequestTarget(zoneNames, selectedZone =>
            {
                DestroyMonsterInZone(selectedZone);
                alreadyChosenZones.Add(selectedZone);

                if (tributesRemaining - 1 > 0)
                    RequestTributes(card, t, zoneName, owner, tributesRemaining - 1, alreadyChosenZones);
                else
                    CompleteNormalSummon(card, t, zoneName, owner);
            });

            if (targetSelectionUI != null)
                targetSelectionUI.ShowForCustom(zoneNames, labels);
        }

        private void CompleteNormalSummon(CardData card, Transform t, string zoneName, PlayerSide owner)
        {
            Debug.Log($"[CardEffectRouter] Summon: {card.displayName} ({owner}'s)");
            PlayFx(summonParticlesPrefab, summonSound, summonVolume, t);

            if (zoneName != null && MonsterFieldState.Instance != null)
                MonsterFieldState.Instance.PlaceMonster(zoneName, card.id);

            TurnManager.Instance?.MarkNormalSummonUsed(owner);
        }

        // --- Step 6: real flip-effect trigger, with actual resolution for Man-Eater Bug ---
        // Man-Eater Bug can target a monster on either side, per the real card.
        // Targets by ZONE (not card id) so duplicate copies of the same card
        // don't collide — see CardDatabase's tribute-selection note.
        private void PlayFlipEffect(CardData card, Transform t, string ownZoneName)
        {
            Debug.Log($"[CardEffectRouter] Flip Effect triggered: {card.displayName}");
            PlayFx(flipEffectParticlesPrefab, flipEffectSound, 0.8f, t);

            if (card.id == "man_eater_bug")
            {
                var candidates = MonsterFieldState.Instance.GetAllMonsters()
                    .Where(m => m.zoneName != ownZoneName) // can't target itself
                    .ToArray();

                if (candidates.Length == 0)
                {
                    Debug.Log("[CardEffectRouter] Man-Eater Bug flipped, but no other monster to destroy.");
                    return;
                }

                var zoneNames = candidates.Select(c => c.zoneName).ToArray();
                var labels = candidates.Select(c => $"{CardDatabase.Get(c.cardId)?.displayName} ({c.zoneName})").ToArray();

                TargetSelectionManager.Instance.RequestTarget(zoneNames, selectedZone =>
                {
                    DestroyMonsterInZone(selectedZone);
                });

                if (targetSelectionUI != null)
                    targetSelectionUI.ShowForCustom(zoneNames, labels);
            }
        }

        // --- Step 7: spell that needs a target — Monster Reborn can target either graveyard, revives to the controller's side ---
        private void PlaySpellTarget(CardData card, Transform t, string zoneName)
        {
            if (card.id == "monster_reborn")
            {
                var controller = OwnerOf(zoneName);
                var graveyardEntries = GraveyardState.Instance.GetAllBothSides().ToArray();

                if (graveyardEntries.Length == 0)
                {
                    Debug.Log("[CardEffectRouter] Monster Reborn has nothing in either graveyard to target.");
                    return;
                }

                var cardIds = graveyardEntries.Select(e => e.cardId).Distinct().ToArray();
                Debug.Log($"[CardEffectRouter] {card.displayName} needs a target — requesting selection.");

                TargetSelectionManager.Instance.RequestTarget(cardIds, selectedId =>
                {
                    var fromSide = GraveyardState.Instance.FindOwner(selectedId);
                    if (fromSide == null)
                    {
                        Debug.LogWarning($"[CardEffectRouter] '{selectedId}' is no longer in any graveyard.");
                        return;
                    }

                    GraveyardState.Instance.Remove(fromSide.Value, selectedId);
                    var landedZone = MonsterFieldState.Instance.ReviveToFirstEmptyZone(selectedId, controller);
                    var target = CardDatabase.Get(selectedId);
                    Debug.Log(landedZone != null
                        ? $"[CardEffectRouter] {target?.displayName} revived into {landedZone} under {controller}'s control. Place the physical card there."
                        : $"[CardEffectRouter] {target?.displayName} could not be revived — {controller} has no empty Monster zone.");
                });

                if (targetSelectionUI != null)
                    targetSelectionUI.ShowFor(cardIds);
            }
        }

        // --- Step 7b: spell that resolves immediately, for whichever side played it ---
        private void PlaySpellSimple(CardData card, Transform t, string zoneName)
        {
            Debug.Log($"[CardEffectRouter] Resolving: {card.displayName} ({OwnerOf(zoneName)}'s)");

            if (card.id == "pot_of_greed")
                DeckState.Instance.Draw(OwnerOf(zoneName), 2);
        }

        // --- Step 8: trigger check when an attack is declared ---
        private void HandleAttackDeclared()
        {
            if (armedTraps.Count == 0) return;

            foreach (var kvp in armedTraps.ToList())
            {
                FireTrap(kvp.Value.card, kvp.Value.t, kvp.Key);
                armedTraps.Remove(kvp.Key);
            }
        }

        // --- Step 8: real trap resolution for Mirror Force / Trap Hole ---
        // A trap always acts against whoever is attacking — the OPPOSITE side
        // from the trap's own controller (you don't Mirror Force yourself).
        private void FireTrap(CardData card, Transform t, string zoneName)
        {
            var trapOwner = OwnerOf(zoneName);
            var targetSide = trapOwner.Opposite();

            Debug.Log($"[CardEffectRouter] Trap triggered: {card.displayName} ({trapOwner}'s trap, hits {targetSide})");
            PlayFx(trapTriggerParticlesPrefab, trapTriggerSound, 0.9f, t);

            switch (card.id)
            {
                case "mirror_force":
                    foreach (var m in MonsterFieldState.Instance.GetAllMonsters(targetSide).ToList())
                        DestroyMonsterInZone(m.zoneName);
                    break;

                case "trap_hole":
                    MonsterFieldState.Instance.DestroyLastSummoned(targetSide);
                    break;
            }
        }

        private void DestroyMonsterInZone(string zoneName)
        {
            var destroyedCardId = MonsterFieldState.Instance.DestroyZone(zoneName);
            if (destroyedCardId == null) return;

            var zoneTransform = FieldZoneManager.Instance != null
                ? FieldZoneManager.Instance.GetZoneTransform(zoneName)
                : null;

            if (zoneTransform != null)
                PlayFx(destroyParticlesPrefab, destroySound, 0.8f, zoneTransform);
        }

        private void PlayFx(ParticleSystem particlesPrefab, AudioClip clip, float volume, Transform t)
        {
            if (particlesPrefab != null)
            {
                var fx = Instantiate(particlesPrefab, t.position, Quaternion.identity, t);
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
            }

            if (clip != null)
                audioSource.PlayOneShot(clip, volume);

            if (particlesPrefab == null && clip == null && placeholderEffectPrefab != null)
                Instantiate(placeholderEffectPrefab, t.position, Quaternion.identity, t);
        }
    }
}
