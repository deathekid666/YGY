using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
#if AR_FOUNDATION_ENABLED
using UnityEngine.XR.ARFoundation;
#endif

namespace YugiohAR.EditorTools
{
    /// <summary>
    /// Run this from the Unity menu: YugiohAR > Auto-Build Scene.
    ///
    /// Builds everything except your AI vision API key: all manager
    /// singletons (including turn structure, life points, and card
    /// recognition), a live phone-camera background (CameraFeedBackground),
    /// a full two-player field (22 zones — 11 per side, each tagged with
    /// its owner), and all UI panels — including one scan button per zone,
    /// split into a Player column and an Opponent column, and a
    /// battle-declare panel for picking a specific attacker/defender.
    /// Fully wired together. Safe to re-run — it won't duplicate objects
    /// that already exist by name.
    ///
    /// Recognition here uses CardVisionScanner (a live camera frame sent to
    /// a vision-capable AI model) rather than Vuforia Image Targets — no
    /// account, no license key, no photographing your 10 cards ahead of
    /// time. See the README for the trade-offs and the Vuforia alternative
    /// if you'd rather go fully offline later.
    ///
    /// What this can NOT do for you: paste your own API key into
    /// CardVisionScanner's apiKey field, and build/deploy the actual Android
    /// APK — that's Unity's Build Settings, not something a script inside
    /// the project can trigger for you. See the README's Android build steps.
    /// </summary>
    public static class SceneAutoSetup
    {
        [MenuItem("YugiohAR/Auto-Build Scene")]
        public static void BuildScene()
        {
            var effectRouterGO = GetOrCreate("CardEffectRouter");
            var effectRouter = GetOrAddComponent<CardEffectRouter>(effectRouterGO);

            var gameFieldGO = GetOrCreate("GameFieldManager");
            var gameField = GetOrAddComponent<GameFieldManager>(gameFieldGO);
            gameField.effectRouter = effectRouter;

            GetOrAddComponent<DeckState>(GetOrCreate("DeckState"));
            GetOrAddComponent<GraveyardState>(GetOrCreate("GraveyardState"));
            GetOrAddComponent<MonsterFieldState>(GetOrCreate("MonsterFieldState"));
            GetOrAddComponent<TargetSelectionManager>(GetOrCreate("TargetSelectionManager"));
            GetOrAddComponent<LifePointsState>(GetOrCreate("LifePointsState"));
            GetOrAddComponent<TurnManager>(GetOrCreate("TurnManager"));

            var scannerGO = GetOrCreate("CardVisionScanner");
            var scanner = GetOrAddComponent<CardVisionScanner>(scannerGO);

            var fieldZoneManagerGO = GetOrCreate("FieldZoneManager");
            var fieldZoneManager = GetOrAddComponent<FieldZoneManager>(fieldZoneManagerGO);
            fieldZoneManager.zones = BuildZones(fieldZoneManagerGO.transform);

            var canvasGO = BuildCanvas();
#if AR_FOUNDATION_ENABLED
            var arCamera = BuildARFoundationRig(fieldZoneManagerGO.transform);
            scanner.arFrameCamera = arCamera;
            Debug.Log("[SceneAutoSetup] AR_FOUNDATION_ENABLED detected — building the real AR rig " +
                      "(plane detection + tap-to-place) instead of the flat WebCamTexture background.");
#else
            var cameraFeed = BuildCameraFeedBackground(canvasGO.transform);
#endif
            var trapPrompt = BuildTrapIdentityPrompt(canvasGO.transform, effectRouter);
            var targetSelect = BuildTargetSelectionUI(canvasGO.transform);
            BuildAttackButton(canvasGO.transform);
            BuildNextPhaseButton(canvasGO.transform);
            BuildLifePointsDisplay(canvasGO.transform);
            BuildScanButtons(canvasGO.transform, scanner, fieldZoneManager.zones);
            BuildDebugRecognitionPanel(canvasGO.transform);
            BuildBattleDeclarePanel(canvasGO.transform);

            effectRouter.trapIdentityPrompt = trapPrompt;
            effectRouter.targetSelectionUI = targetSelect;

            EditorUtility.SetDirty(effectRouter);
            EditorUtility.SetDirty(gameField);
            EditorUtility.SetDirty(fieldZoneManager);
            EditorUtility.SetDirty(scanner);
#if !AR_FOUNDATION_ENABLED
            EditorUtility.SetDirty(cameraFeed);
#endif

            Debug.Log("[SceneAutoSetup] Done. Managers (turn structure, life points, vision-based " +
                      "recognition), a live camera-feed background, a full two-player field " +
                      "(22 zones), scan buttons for both sides, a battle-declare panel, and UI " +
                      "panels are built and wired. Next: paste your Anthropic API key into " +
                      "CardVisionScanner's apiKey field (see README) — that's the one piece no " +
                      "script can do for you.");
        }

        // ---------- Zones ----------

        private static FieldZone[] BuildZones(Transform parent)
        {
            // Two full mats — one per player — each with 5 Monster zones,
            // 5 Spell/Trap zones, and 1 Graveyard slot. The Opponent's mat
            // sits further along Z, mirroring how two real mats face each
            // other across a table. Reposition these in the Inspector to
            // match your actual physical setup.
            var specs = new System.Collections.Generic.List<(string name, ZoneType type, PlayerSide owner, Vector3 pos)>();

            void AddRow(PlayerSide side, ZoneType type, float zOffset)
            {
                for (int i = 1; i <= 5; i++)
                {
                    float x = -0.4f + (i - 1) * 0.2f;
                    specs.Add(($"{side}_{type}_{i}", type, side, new Vector3(x, 0, zOffset)));
                }
            }

            // Player's mat: nearer (smaller Z). Opponent's mat: further (larger Z).
            AddRow(PlayerSide.Player, ZoneType.SpellTrap, 0.0f);
            AddRow(PlayerSide.Player, ZoneType.Monster, 0.2f);
            specs.Add(($"{PlayerSide.Player}_Graveyard_1", ZoneType.Graveyard, PlayerSide.Player, new Vector3(0.6f, 0, 0.1f)));

            AddRow(PlayerSide.Opponent, ZoneType.Monster, 0.6f);
            AddRow(PlayerSide.Opponent, ZoneType.SpellTrap, 0.8f);
            specs.Add(($"{PlayerSide.Opponent}_Graveyard_1", ZoneType.Graveyard, PlayerSide.Opponent, new Vector3(0.6f, 0, 0.7f)));

            var zones = new FieldZone[specs.Count];
            for (int i = 0; i < specs.Count; i++)
            {
                var (name, type, owner, pos) = specs[i];
                var zoneGO = GetOrCreate(name, parent);
                zoneGO.transform.localPosition = pos;

                var col = GetOrAddComponent<BoxCollider>(zoneGO);
                col.isTrigger = true;
                col.size = new Vector3(0.18f, 0.1f, 0.18f);

                var zone = GetOrAddComponent<FieldZone>(zoneGO);
                zone.zoneName = name;
                zone.zoneType = type;
                zone.owner = owner;
                zones[i] = zone;

                BuildZoneOutline(zoneGO.transform, type);
            }
            return zones;
        }

        private static void BuildZoneOutline(Transform zoneParent, ZoneType type)
        {
            var existing = zoneParent.Find("Outline");
            if (existing != null) return; // idempotent

            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outline.name = "Outline";
            outline.transform.SetParent(zoneParent, false);
            outline.transform.localPosition = new Vector3(0, 0.001f, 0); // just above the surface
            outline.transform.localRotation = Quaternion.Euler(90, 0, 0); // lie flat
            outline.transform.localScale = new Vector3(0.16f, 0.16f, 1f);

            var collider = outline.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider); // the zone's own BoxCollider handles detection

            var color = type switch
            {
                ZoneType.Monster => new Color(0.43f, 0.84f, 0.77f, 0.25f),   // teal, matches earlier mockups
                ZoneType.SpellTrap => new Color(0.95f, 0.65f, 0.3f, 0.22f),  // orange
                _ => new Color(1f, 1f, 1f, 0.12f),                          // graveyard / other
            };

            var renderer = outline.GetComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Sprites/Default")) { color = color };
            renderer.sharedMaterial = material;
        }

        // ---------- UI ----------

        private static GameObject BuildCanvas()
        {
            var existing = GameObject.Find("YugiohAR_Canvas");
            if (existing != null) return existing;

            var canvasGO = new GameObject("YugiohAR_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            return canvasGO;
        }

        private static CameraFeedBackground BuildCameraFeedBackground(Transform canvasParent)
        {
            var existing = canvasParent.Find("CameraFeedBackground");
            GameObject go = existing != null ? existing.gameObject
                : new GameObject("CameraFeedBackground", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            go.transform.SetParent(canvasParent, false);
            go.transform.SetAsFirstSibling(); // render behind every other UI element

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            var feed = GetOrAddComponent<CameraFeedBackground>(go);
            feed.display = go.GetComponent<RawImage>();
            return feed;
        }

        private static TrapIdentityPromptUI BuildTrapIdentityPrompt(Transform canvasParent, CardEffectRouter router)
        {
            var panel = BuildPanel(canvasParent, "TrapIdentityPanel", new Vector2(0, 100));
            var buttonPrefab = BuildButtonPrefabInstance(panel.transform, "TrapOptionButtonTemplate");
            buttonPrefab.SetActive(false);

            var prompt = GetOrAddComponent<TrapIdentityPromptUI>(panel);
            prompt.panelRoot = panel;
            prompt.buttonPrefab = buttonPrefab.GetComponent<Button>();
            prompt.buttonContainer = panel.transform;
            prompt.effectRouter = router;

            panel.SetActive(false);
            return prompt;
        }

        private static TargetSelectionUI BuildTargetSelectionUI(Transform canvasParent)
        {
            var panel = BuildPanel(canvasParent, "TargetSelectPanel", new Vector2(0, -100));
            var buttonPrefab = BuildButtonPrefabInstance(panel.transform, "TargetOptionButtonTemplate");
            buttonPrefab.SetActive(false);

            var ui = GetOrAddComponent<TargetSelectionUI>(panel);
            ui.panelRoot = panel;
            ui.buttonPrefab = buttonPrefab.GetComponent<Button>();
            ui.buttonContainer = panel.transform;

            panel.SetActive(false);
            return ui;
        }

        private static void BuildAttackButton(Transform canvasParent)
        {
            var existing = canvasParent.Find("DeclareAttackButton");
            GameObject buttonGO = existing != null ? existing.gameObject
                : BuildButtonPrefabInstance(canvasParent, "DeclareAttackButton");

            var rt = buttonGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 60);

            SetButtonLabel(buttonGO, "Trigger Traps (test)");
            GetOrAddComponent<AttackDeclareButton>(buttonGO);
        }

        private static void BuildNextPhaseButton(Transform canvasParent)
        {
            var existing = canvasParent.Find("NextPhaseButton");
            GameObject buttonGO = existing != null ? existing.gameObject
                : BuildButtonPrefabInstance(canvasParent, "NextPhaseButton");

            var rt = buttonGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 110);

            SetButtonLabel(buttonGO, "Next Phase");
            GetOrAddComponent<NextPhaseButton>(buttonGO);
        }

        private static void BuildLifePointsDisplay(Transform canvasParent)
        {
            var existing = canvasParent.Find("LifePointsDisplay");
            GameObject displayGO = existing != null ? existing.gameObject : new GameObject("LifePointsDisplay", typeof(RectTransform));
            displayGO.transform.SetParent(canvasParent, false);

            var rt = displayGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(240, 60);

            var playerText = FindOrCreateText(displayGO.transform, "PlayerLifeText", new Vector2(0, -10));
            var opponentText = FindOrCreateText(displayGO.transform, "OpponentLifeText", new Vector2(0, -35));

            var ui = GetOrAddComponent<LifePointsUI>(displayGO);
            ui.playerLifeText = playerText;
            ui.opponentLifeText = opponentText;
        }

        private static Text FindOrCreateText(Transform parent, string name, Vector2 anchoredPos)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Text>();

            var textGO = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(parent, false);
            var rt = textGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(220, 25);

            var text = textGO.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private static void BuildScanButtons(Transform canvasParent, CardVisionScanner scanner, FieldZone[] zones)
        {
            BuildScanButtonColumn(canvasParent, scanner, zones, PlayerSide.Player,
                "ScanButtonsPanel_Player", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16, 16));
            BuildScanButtonColumn(canvasParent, scanner, zones, PlayerSide.Opponent,
                "ScanButtonsPanel_Opponent", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16, 16));
        }

        private static void BuildScanButtonColumn(Transform canvasParent, CardVisionScanner scanner, FieldZone[] zones,
            PlayerSide side, string panelName, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos)
        {
            var sideZones = System.Linq.Enumerable.Where(zones, z => z.owner == side && z.zoneType != ZoneType.Graveyard);
            var sideZonesArray = System.Linq.Enumerable.ToArray(sideZones);

            var existing = canvasParent.Find(panelName);
            GameObject panelGO = existing != null ? existing.gameObject
                : new GameObject(panelName, typeof(RectTransform), typeof(VerticalLayoutGroup));
            panelGO.transform.SetParent(canvasParent, false);

            var rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(90, 32 * sideZonesArray.Length);

            var layout = panelGO.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 3;
            layout.childForceExpandHeight = false;

            foreach (var zone in sideZonesArray)
            {
                var buttonGO = BuildButtonPrefabInstance(panelGO.transform, $"Scan_{zone.zoneName}");
                buttonGO.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 28);
                SetButtonLabel(buttonGO, zone.zoneName.Replace($"{side}_", "").Replace("_", " "));

                var scanButton = GetOrAddComponent<ZoneScanButton>(buttonGO);
                scanButton.zoneName = zone.zoneName;
                scanButton.scanner = scanner;
            }
        }

        private static void BuildBattleDeclarePanel(Transform canvasParent)
        {
            var panel = BuildPanel(canvasParent, "BattleDeclarePanel", new Vector2(0, 160));
            var vlayout = panel.GetComponent<VerticalLayoutGroup>();
            vlayout.spacing = 6;

            var attackerCycler = BuildCyclerRow(panel.transform, "AttackerCycler");
            var defenderCycler = BuildCyclerRow(panel.transform, "DefenderCycler");

            var battleUI = GetOrAddComponent<BattleDeclareUI>(panel);
            battleUI.attackerCycler = attackerCycler;
            battleUI.defenderCycler = defenderCycler;

            var declareBtn = BuildButtonPrefabInstance(panel.transform, "DeclareBattleAttackButton");
            SetButtonLabel(declareBtn, "Declare Attack (Battle)");
            declareBtn.GetComponent<Button>().onClick.AddListener(battleUI.DeclareAttack);
        }

        private static void BuildDebugRecognitionPanel(Transform canvasParent)
        {
            var panel = BuildPanel(canvasParent, "DebugRecognitionPanel", new Vector2(0, 220));
            var vlayout = panel.GetComponent<VerticalLayoutGroup>();
            vlayout.spacing = 8;

            var zoneCycler = BuildCyclerRow(panel.transform, "ZoneCycler");
            var cardCycler = BuildCyclerRow(panel.transform, "CardCycler");

            var debugUI = GetOrAddComponent<DebugRecognitionUI>(panel);
            debugUI.zoneCycler = zoneCycler;
            debugUI.cardCycler = cardCycler;

            var scanBtn = BuildButtonPrefabInstance(panel.transform, "SimulateScanButton");
            SetButtonLabel(scanBtn, "Simulate scan");
            scanBtn.GetComponent<Button>().onClick.AddListener(debugUI.SimulateScan);

            var setBtn = BuildButtonPrefabInstance(panel.transform, "SimulateSetFaceDownButton");
            SetButtonLabel(setBtn, "Simulate set (face-down)");
            setBtn.GetComponent<Button>().onClick.AddListener(debugUI.SimulateSetFaceDown);

            var removeBtn = BuildButtonPrefabInstance(panel.transform, "SimulateRemoveButton");
            SetButtonLabel(removeBtn, "Simulate remove");
            removeBtn.GetComponent<Button>().onClick.AddListener(debugUI.SimulateRemove);
        }

        private static OptionCycler BuildCyclerRow(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<OptionCycler>();

            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 32);
            var hlayout = row.GetComponent<HorizontalLayoutGroup>();
            hlayout.spacing = 4;
            hlayout.childForceExpandWidth = false;
            hlayout.childAlignment = TextAnchor.MiddleCenter;

            var prevBtn = BuildButtonPrefabInstance(row.transform, "Prev");
            prevBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(32, 28);
            SetButtonLabel(prevBtn, "<");

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(row.transform, false);
            labelGO.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 28);
            var label = labelGO.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "-";

            var nextBtn = BuildButtonPrefabInstance(row.transform, "Next");
            nextBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(32, 28);
            SetButtonLabel(nextBtn, ">");

            var cycler = row.AddComponent<OptionCycler>();
            cycler.label = label;
            cycler.prevButton = prevBtn.GetComponent<Button>();
            cycler.nextButton = nextBtn.GetComponent<Button>();
            return cycler;
        }

        // ---------- AR Foundation (optional, guarded) ----------

#if AR_FOUNDATION_ENABLED
        private static Camera BuildARFoundationRig(Transform fieldRootTransform)
        {
            var sessionGO = GetOrCreate("AR Session");
            GetOrAddComponent<ARSession>(sessionGO);

            var originGO = GetOrCreate("AR Session Origin");
            var origin = GetOrAddComponent<ARSessionOrigin>(originGO);

            var camTransform = originGO.transform.Find("AR Camera");
            GameObject camGO;
            if (camTransform != null)
            {
                camGO = camTransform.gameObject;
            }
            else
            {
                camGO = new GameObject("AR Camera", typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground));
                camGO.transform.SetParent(originGO.transform, false);
                camGO.tag = "MainCamera";
                // NOTE: for the camera to actually move with the device, it
                // also needs a pose driver — either UnityEngine.SpatialTracking
                // .TrackedPoseDriver (legacy Input Manager) or the Input
                // System package's TrackedPoseDriver, depending on which
                // Input System your project uses. Add whichever matches in
                // Project Settings > Player > Active Input Handling — this
                // script doesn't guess which one, since guessing wrong breaks
                // the build.
            }

            var arCam = camGO.GetComponent<Camera>();
            origin.camera = arCam;

            var planeManager = GetOrAddComponent<ARPlaneManager>(originGO);
            GetOrAddComponent<ARRaycastManager>(originGO);

            var placer = GetOrAddComponent<ARFieldPlacer>(originGO);
            placer.fieldRoot = fieldRootTransform;
            placer.planeManager = planeManager;

            return arCam;
        }
#endif

        private static GameObject BuildPanel(Transform parent, string name, Vector2 anchoredPos)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var panel = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(parent, false);

            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260, 200);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;

            var img = panel.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 6;
            layout.childForceExpandHeight = false;

            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return panel;
        }

        private static GameObject BuildButtonPrefabInstance(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var buttonGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(parent, false);
            buttonGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 40);
            buttonGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            SetButtonLabel(buttonGO, "Option");
            return buttonGO;
        }

        private static void SetButtonLabel(GameObject buttonGO, string label)
        {
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(buttonGO.transform, false);
            var rt = textGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var text = textGO.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ---------- Small helpers ----------

        private static GameObject GetOrCreate(string name, Transform parent = null)
        {
            Transform found = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
            if (found != null) return found.gameObject;

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
