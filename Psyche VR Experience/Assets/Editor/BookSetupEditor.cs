using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PsycheVR.Gameplay;

namespace PsycheVR.EditorTools
{
    /// <summary>
    /// Editor utility to configure the InstructionManualBook prefab with the
    /// Rigidbody + ConfigurableJoint + BookPageGrabbable architecture.
    ///
    /// Removes any pre-existing ArticulationBody / ArticulationGrabbable components,
    /// re-parents the 6 page/cover pieces from SpinePivot to the empty
    /// InstructionManualBook root, and configures every component per the spec.
    ///
    /// Idempotent — safe to re-run. Operates on whichever InstructionManualBook
    /// it finds first: prefab stage in isolation, or scene instance.
    /// </summary>
    public static class BookSetupEditor
    {
        private const string MenuPath = "Tools/Setup Book Hierarchy (Rigidbody)";
        private const string GrabSettingsGuid = "d224529470e8b024d8a5ff0c6bad924d";

        // Names that should be configured as page/cover pieces (joint children of the spine)
        private static readonly string[] PageNames =
        {
            "Cover_Back", "Cover_Front",
            "Page_001", "Page_002", "Page_003", "Page_004",
        };

        [MenuItem(MenuPath)]
        public static void SetupBookHierarchy()
        {
            GameObject root = FindBookRoot();
            if (root == null)
            {
                Debug.LogError("[BookSetupEditor] Could not find an InstructionManualBook GameObject. Open the prefab in isolation, or load a scene that contains the book.");
                return;
            }

            Transform spinePivot = root.transform.Find("SpinePivot");
            if (spinePivot == null)
            {
                Debug.LogError("[BookSetupEditor] InstructionManualBook is missing its SpinePivot child.");
                return;
            }

            // Register the entire hierarchy with Undo so the user can Ctrl+Z if anything goes wrong
            Undo.RegisterFullObjectHierarchyUndo(root, "Setup Book Hierarchy");

            // Step 1: Remove legacy components from all descendants
            RemoveLegacyComponents(root);

            // Step 2: Re-parent the 6 page/cover pieces from SpinePivot to root.
            // Collect refs first because changing parent mutates the iterator.
            // worldPositionStays: true keeps each child at its existing world pose,
            // which is correct here because SpinePivot is at identity local transform
            // under InstructionManualBook in the authored prefab. If SpinePivot ever
            // gets a non-identity local transform, this assumption needs revisiting.
            var toReparent = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in spinePivot)
            {
                if (System.Array.IndexOf(PageNames, child.name) >= 0)
                    toReparent.Add(child);
            }
            foreach (var t in toReparent)
            {
                t.SetParent(root.transform, worldPositionStays: true);
            }

            // Step 3: Configure SpinePivot
            ConfigureSpinePivot(spinePivot.gameObject);
            Rigidbody spineRb = spinePivot.GetComponent<Rigidbody>();
            if (spineRb == null)
            {
                Debug.LogError("[BookSetupEditor] Failed to obtain Rigidbody on SpinePivot. Aborting page setup.");
                return;
            }

            // Step 4: Configure each page/cover (now siblings of SpinePivot under root)
            int configured = 0;
            foreach (string name in PageNames)
            {
                Transform t = root.transform.Find(name);
                if (t == null)
                {
                    Debug.LogError($"[BookSetupEditor] Missing expected child: {name}");
                    continue;
                }
                ConfigurePageOrCover(t.gameObject, spineRb);
                configured++;
            }

            // Step 5: Mark dirty + save
            EditorUtility.SetDirty(root);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                EditorSceneManager.MarkSceneDirty(stage.scene);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
            }

            Debug.Log($"[BookSetupEditor] Setup complete. Configured SpinePivot + {configured} page/cover pieces. Save the scene/prefab to persist.");
        }

        private static GameObject FindBookRoot()
        {
            // Try open prefab stage first
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null && stage.prefabContentsRoot.name == "InstructionManualBook")
                return stage.prefabContentsRoot;

            // Fall back to scene instance
            return GameObject.Find("InstructionManualBook");
        }

        private static void RemoveLegacyComponents(GameObject root)
        {
            // ArticulationGrabbable must be removed FIRST because it has
            // [RequireComponent(typeof(ArticulationBody))]. Unity refuses to remove
            // the ArticulationBody while the dependent script is still attached.
            // Detect by type name so this editor script doesn't depend on the .cs
            // file existing at compile time.
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "ArticulationGrabbable")
                    Object.DestroyImmediate(mb);
            }

            // Now ArticulationBody can be removed safely.
            foreach (var ab in root.GetComponentsInChildren<ArticulationBody>(true))
            {
                Object.DestroyImmediate(ab);
            }
        }

        private static void ConfigureSpinePivot(GameObject go)
        {
            var rb = EnsureComponent<Rigidbody>(go);
            rb.mass = 0.5f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 20;
            rb.solverVelocityIterations = 4;
            rb.linearDamping = 5f;
            rb.angularDamping = 5f;

            // BoxCollider should already exist with authored size — leave its dimensions alone
            var box = EnsureComponent<BoxCollider>(go);

            var grabbable = EnsureComponent<PsycheGrabbable>(go);
            AssignGrabSettings(grabbable);
            AssignInteractableColliders(grabbable, box);
        }

        private static void ConfigurePageOrCover(GameObject go, Rigidbody spineRb)
        {
            var rb = EnsureComponent<Rigidbody>(go);
            rb.mass = 0.05f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 20;
            rb.solverVelocityIterations = 4;
            rb.linearDamping = 2f;
            rb.angularDamping = 8f;

            var box = EnsureComponent<BoxCollider>(go);

            var joint = EnsureComponent<ConfigurableJoint>(go);

            // Set axis/anchor BEFORE connectedBody so Unity's internal connectedAnchor
            // recomputation (triggered when connectedBody changes) uses the right values.

            // Anchor at body origin. The FBX pivot is placed at the spine line, so
            // (0, 0, 0) in body-local IS the hinge point. The page collider extends
            // away from the spine in +X (box.center.x ≈ 0.132), but the body origin
            // remains at the spine.
            joint.anchor = Vector3.zero;

            // Hinge axis is body-local X. The pages have parent rotation X=270° at rest,
            // which leaves body-local X aligned with world X (the X rotation only affects
            // Y and Z). So pages rotate around the world X axis when flipped — a horizontal
            // axis, which matches a book lying flat on a desk with the spine running E-W.
            // (Earlier (0,0,1) put the hinge along world Y, which is vertical and wrong.)
            joint.axis = new Vector3(1f, 0f, 0f);
            joint.secondaryAxis = new Vector3(0f, 1f, 0f);

            joint.connectedBody = spineRb;
            joint.autoConfigureConnectedAnchor = true;

            // Lock all linear motion
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            // Single hinge degree of freedom: angularX
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            // Joint limits +/- 80 degrees
            var lowLimit = joint.lowAngularXLimit;
            lowLimit.limit = -80f;
            joint.lowAngularXLimit = lowLimit;

            var highLimit = joint.highAngularXLimit;
            highLimit.limit = 80f;
            joint.highAngularXLimit = highLimit;

            // No spring at limits
            var limitSpring = joint.angularXLimitSpring;
            limitSpring.spring = 0f;
            limitSpring.damper = 0f;
            joint.angularXLimitSpring = limitSpring;

            // Drive starts at 0 spring + small damper. BookPageGrabbable raises spring on grab.
            var drive = joint.angularXDrive;
            drive.positionSpring = 0f;
            drive.positionDamper = 2f;
            drive.maximumForce = Mathf.Infinity;
            joint.angularXDrive = drive;

            // Projection — the fix for "rubbery" joint flex under load
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.001f;
            joint.projectionAngle = 1f;

            joint.enablePreprocessing = false;

            // Add the page-specific grab class (which itself inherits from PsycheGrabbable)
            var grabbable = EnsureComponent<BookPageGrabbable>(go);
            AssignGrabSettings(grabbable);
            AssignInteractableColliders(grabbable, box);
        }

        private static void AssignGrabSettings(PsycheGrabbable grabbable)
        {
            string path = AssetDatabase.GUIDToAssetPath(GrabSettingsGuid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[BookSetupEditor] GrabSettings asset not found by GUID. Falling back to type search.");
                var found = AssetDatabase.FindAssets("t:GrabSettings");
                if (found.Length > 0) path = AssetDatabase.GUIDToAssetPath(found[0]);
            }
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[BookSetupEditor] No GrabSettings asset in the project. Assign one manually on each grabbable.");
                return;
            }
            var settings = AssetDatabase.LoadAssetAtPath<GrabSettings>(path);
            if (settings == null)
            {
                Debug.LogError($"[BookSetupEditor] Failed to load GrabSettings at {path}.");
                return;
            }

            var so = new SerializedObject(grabbable);
            var prop = so.FindProperty("grabSettings");
            if (prop == null)
            {
                Debug.LogWarning("[BookSetupEditor] PsycheGrabbable has no serialized field named 'grabSettings'. Field name may have changed — check PsycheGrabbable.cs.");
                return;
            }
            prop.objectReferenceValue = settings;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Explicitly populates the XRBaseInteractable.m_Colliders list so XRI knows
        /// which colliders represent this interactable. The previous ArticulationGrabbable
        /// setup did this; without it the page interactables are unreachable by the
        /// hand interactor (auto-discovery is unreliable when m_Colliders is initialized
        /// to an empty list rather than null).
        /// </summary>
        private static void AssignInteractableColliders(PsycheGrabbable grabbable, Collider collider)
        {
            if (collider == null) return;
            var so = new SerializedObject(grabbable);
            var prop = so.FindProperty("m_Colliders");
            if (prop == null)
            {
                Debug.LogWarning("[BookSetupEditor] Grabbable has no serialized field 'm_Colliders'.");
                return;
            }
            prop.ClearArray();
            prop.InsertArrayElementAtIndex(0);
            prop.GetArrayElementAtIndex(0).objectReferenceValue = collider;
            so.ApplyModifiedProperties();
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }
    }
}
