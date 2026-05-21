using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Player))]
public class PlayerEditor : Editor
{
    private bool showMovement = true;
    private bool showFeel = true;
    private bool showDashCombat = false;
    private bool showInteraction = false;
    private bool showCollision = true;
    private bool showRuntime = true;

    private SerializedProperty moveSpeedProp;
    private SerializedProperty jumpForceProp;
    private SerializedProperty wallJumpForceProp;
    private SerializedProperty inAirMoveMultiplierProp;
    private SerializedProperty wallSlideSlowMultiplierProp;
    private SerializedProperty dashDurationProp;
    private SerializedProperty dashSpeedProp;
    private SerializedProperty dashCooldownProp;

    private SerializedProperty groundAccelerationProp;
    private SerializedProperty groundDecelerationProp;
    private SerializedProperty airAccelerationProp;
    private SerializedProperty airDecelerationProp;
    private SerializedProperty coyoteTimeProp;
    private SerializedProperty jumpBufferTimeProp;
    private SerializedProperty jumpCutGravityMultiplierProp;
    private SerializedProperty fallGravityMultiplierProp;
    private SerializedProperty jumpHangGravityMultiplierProp;
    private SerializedProperty jumpHangVelocityThresholdProp;
    private SerializedProperty maxFallSpeedProp;
    private SerializedProperty maxFastFallSpeedProp;
    private SerializedProperty wallSlideSpeedProp;
    private SerializedProperty wallJumpControlLockTimeProp;

    private SerializedProperty allowAttackInputProp;
    private SerializedProperty attackVelocityProp;
    private SerializedProperty jumpAttackVelocityProp;
    private SerializedProperty attackVelocityDurationProp;
    private SerializedProperty comboResetTimeProp;

    private SerializedProperty interactHoldDurationProp;
    private SerializedProperty interactMoveSlowMultiplierProp;

    private SerializedProperty groundCheckDistanceProp;
    private SerializedProperty wallCheckDistanceProp;
    private SerializedProperty whatIsGroundProp;
    private SerializedProperty primaryWallCheckProp;
    private SerializedProperty secondaryWallCheckProp;

    private GUIStyle sectionStyle;

    private void OnEnable()
    {
        moveSpeedProp = serializedObject.FindProperty("moveSpeed");
        jumpForceProp = serializedObject.FindProperty("jumpForce");
        wallJumpForceProp = serializedObject.FindProperty("wallJumpForce");
        inAirMoveMultiplierProp = serializedObject.FindProperty("inAirMoveMultiplier");
        wallSlideSlowMultiplierProp = serializedObject.FindProperty("wallSlideSlowMultiplier");
        dashDurationProp = serializedObject.FindProperty("dashDuration");
        dashSpeedProp = serializedObject.FindProperty("dashSpeed");
        dashCooldownProp = serializedObject.FindProperty("dashCooldown");

        groundAccelerationProp = serializedObject.FindProperty("groundAcceleration");
        groundDecelerationProp = serializedObject.FindProperty("groundDeceleration");
        airAccelerationProp = serializedObject.FindProperty("airAcceleration");
        airDecelerationProp = serializedObject.FindProperty("airDeceleration");
        coyoteTimeProp = serializedObject.FindProperty("coyoteTime");
        jumpBufferTimeProp = serializedObject.FindProperty("jumpBufferTime");
        jumpCutGravityMultiplierProp = serializedObject.FindProperty("jumpCutGravityMultiplier");
        fallGravityMultiplierProp = serializedObject.FindProperty("fallGravityMultiplier");
        jumpHangGravityMultiplierProp = serializedObject.FindProperty("jumpHangGravityMultiplier");
        jumpHangVelocityThresholdProp = serializedObject.FindProperty("jumpHangVelocityThreshold");
        maxFallSpeedProp = serializedObject.FindProperty("maxFallSpeed");
        maxFastFallSpeedProp = serializedObject.FindProperty("maxFastFallSpeed");
        wallSlideSpeedProp = serializedObject.FindProperty("wallSlideSpeed");
        wallJumpControlLockTimeProp = serializedObject.FindProperty("wallJumpControlLockTime");

        allowAttackInputProp = serializedObject.FindProperty("allowAttackInput");
        attackVelocityProp = serializedObject.FindProperty("attackVelocity");
        jumpAttackVelocityProp = serializedObject.FindProperty("jumpAttackVelocity");
        attackVelocityDurationProp = serializedObject.FindProperty("attackVelocityDuration");
        comboResetTimeProp = serializedObject.FindProperty("comboResetTime");

        interactHoldDurationProp = serializedObject.FindProperty("interactHoldDuration");
        interactMoveSlowMultiplierProp = serializedObject.FindProperty("interactMoveSlowMultiplier");

        groundCheckDistanceProp = serializedObject.FindProperty("groundCheckDistance");
        wallCheckDistanceProp = serializedObject.FindProperty("wallCheckDistance");
        whatIsGroundProp = serializedObject.FindProperty("whatIsGround");
        primaryWallCheckProp = serializedObject.FindProperty("primaryWallCheck");
        secondaryWallCheckProp = serializedObject.FindProperty("secondaryWallCheck");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureStyles();

        Player player = (Player)target;

        EditorGUILayout.HelpBox("Tweak the controller here, then play-test in short loops. Start with Movement Feel before touching jump force or wall values.", MessageType.Info);
        EditorGUILayout.Space(4f);

        DrawQuickActions(player);
        DrawMovementSection();
        DrawFeelSection();
        DrawDashCombatSection();
        DrawInteractionSection();
        DrawCollisionSection(player);
        DrawRuntimeSection(player);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawQuickActions(Player player)
    {
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("Quick Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Auto-Find Checks"))
        {
            AutoAssignChecks(player);
        }

        using (new EditorGUI.DisabledScope(player.anim == null))
        {
            if (GUILayout.Button("Ping Animator"))
            {
                EditorGUIUtility.PingObject(player.anim);
            }
        }

        using (new EditorGUI.DisabledScope(player.rb == null))
        {
            if (GUILayout.Button("Ping Rigidbody"))
            {
                EditorGUIUtility.PingObject(player.rb);
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawMovementSection()
    {
        showMovement = EditorGUILayout.BeginFoldoutHeaderGroup(showMovement, "Core Movement");
        if (showMovement)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.PropertyField(moveSpeedProp, new GUIContent("Move Speed"));
            EditorGUILayout.PropertyField(jumpForceProp, new GUIContent("Jump Force"));
            EditorGUILayout.PropertyField(wallJumpForceProp, new GUIContent("Wall Jump Force"));
            EditorGUILayout.Slider(inAirMoveMultiplierProp, 0f, 1f, new GUIContent("Air Move Multiplier"));
            EditorGUILayout.Slider(wallSlideSlowMultiplierProp, 0f, 1f, new GUIContent("Legacy Wall Slide Slow"));
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawFeelSection()
    {
        showFeel = EditorGUILayout.BeginFoldoutHeaderGroup(showFeel, "Movement Feel");
        if (showFeel)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("Acceleration", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(groundAccelerationProp);
            EditorGUILayout.PropertyField(groundDecelerationProp);
            EditorGUILayout.PropertyField(airAccelerationProp);
            EditorGUILayout.PropertyField(airDecelerationProp);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Forgiveness", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(coyoteTimeProp);
            EditorGUILayout.PropertyField(jumpBufferTimeProp);
            EditorGUILayout.PropertyField(wallJumpControlLockTimeProp);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Gravity", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(jumpCutGravityMultiplierProp);
            EditorGUILayout.PropertyField(fallGravityMultiplierProp);
            EditorGUILayout.PropertyField(jumpHangGravityMultiplierProp);
            EditorGUILayout.PropertyField(jumpHangVelocityThresholdProp);
            EditorGUILayout.PropertyField(maxFallSpeedProp);
            EditorGUILayout.PropertyField(maxFastFallSpeedProp);
            EditorGUILayout.PropertyField(wallSlideSpeedProp);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawDashCombatSection()
    {
        showDashCombat = EditorGUILayout.BeginFoldoutHeaderGroup(showDashCombat, "Dash And Combat");
        if (showDashCombat)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.PropertyField(allowAttackInputProp, new GUIContent("Allow Attack Input"));
            EditorGUILayout.PropertyField(dashDurationProp);
            EditorGUILayout.PropertyField(dashSpeedProp);
            EditorGUILayout.PropertyField(dashCooldownProp);
            EditorGUILayout.PropertyField(attackVelocityProp, new GUIContent("Attack Velocity"), includeChildren: false);
            if (attackVelocityProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < attackVelocityProp.arraySize; i++)
                {
                    SerializedProperty element = attackVelocityProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(element, new GUIContent($"Attack Velocity {i + 1}"));
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(jumpAttackVelocityProp);
            EditorGUILayout.PropertyField(attackVelocityDurationProp);
            EditorGUILayout.PropertyField(comboResetTimeProp);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawInteractionSection()
    {
        showInteraction = EditorGUILayout.BeginFoldoutHeaderGroup(showInteraction, "Interaction");
        if (showInteraction)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.PropertyField(interactHoldDurationProp);
            EditorGUILayout.Slider(interactMoveSlowMultiplierProp, 0f, 1f);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawCollisionSection(Player player)
    {
        showCollision = EditorGUILayout.BeginFoldoutHeaderGroup(showCollision, "Collision Checks");
        if (showCollision)
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.PropertyField(groundCheckDistanceProp);
            EditorGUILayout.PropertyField(wallCheckDistanceProp);
            EditorGUILayout.PropertyField(whatIsGroundProp);
            EditorGUILayout.PropertyField(primaryWallCheckProp);
            EditorGUILayout.PropertyField(secondaryWallCheckProp);

            if (player.transform.childCount == 0)
            {
                EditorGUILayout.HelpBox("This player has no child transforms. Create wall check children and assign them here.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4f);
    }

    private void DrawRuntimeSection(Player player)
    {
        showRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntime, "Runtime Debug");
        if (showRuntime)
        {
            EditorGUILayout.BeginVertical(sectionStyle);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Current State", player.CurrentStateName);
                EditorGUILayout.Toggle("Ground Detected", player.groundDetected);
                EditorGUILayout.Toggle("Wall Detected", player.wallDetected);
                EditorGUILayout.Toggle("Jump Held", player.IsJumpHeld);
                EditorGUILayout.Vector2Field("Move Input", player.moveInput);
                EditorGUILayout.FloatField("Coyote Counter", player.CurrentCoyoteTime);
                EditorGUILayout.FloatField("Jump Buffer", player.CurrentJumpBuffer);
                EditorGUILayout.FloatField("Wall Jump Lock", player.CurrentWallJumpLock);
                EditorGUILayout.FloatField("Dash Cooldown", player.CurrentDashCooldown);

                if (Application.isPlaying && player.rb != null)
                {
                    EditorGUILayout.Vector2Field("Velocity", player.rb.linearVelocity);
                }
            }

            if (Application.isPlaying == false)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live movement state, timers, and velocity.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void AutoAssignChecks(Player player)
    {
        Transform primary = FindChildByName(player.transform, "PrimaryWallCheck", "WallCheckPrimary", "LeftWallCheck");
        Transform secondary = FindChildByName(player.transform, "SecondaryWallCheck", "WallCheckSecondary", "RightWallCheck");

        if (primary != null)
        {
            primaryWallCheckProp.objectReferenceValue = primary;
        }

        if (secondary != null)
        {
            secondaryWallCheckProp.objectReferenceValue = secondary;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(player);
    }

    private static Transform FindChildByName(Transform root, params string[] names)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (string expectedName in names)
        {
            foreach (Transform child in children)
            {
                if (child == root)
                {
                    continue;
                }

                if (child.name.Equals(expectedName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private void EnsureStyles()
    {
        if (sectionStyle != null)
        {
            return;
        }

        sectionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };
    }
}
