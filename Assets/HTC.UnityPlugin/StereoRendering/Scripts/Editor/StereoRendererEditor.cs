//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

using UnityEditor;
using UnityEngine;

namespace HTC.UnityPlugin.StereoRendering
{
    [CustomEditor(typeof(StereoRenderer))]
    public class StereoRendererEditor : Editor
    {
        private SerializedProperty stereoCameraHeadProp;
        private SerializedProperty stereoCameraEyeProp;
        private SerializedProperty anchorTransformProp;
        private SerializedProperty canvasOriginProp;
        private SerializedProperty shouldRenderProp;
        private SerializedProperty textureResProp;
        private SerializedProperty ignoreWhenRenderProp;
        private SerializedProperty isMirrorProp;

        private static float cubeSize = 0.4f;
        private static Color anchorColor = new Color(0.141f, 0.741f, 1.0f, 0.8f);
        private static Color originColor = new Color(1.0f, 0.141f, 0.38f, 0.8f);

        /////////////////////////////////////////////////////////////////////////////////

        public void OnEnable()
        {
            if (target == null || serializedObject == null) return;

            stereoCameraHeadProp = serializedObject.FindProperty("stereoCameraHead");
            stereoCameraEyeProp = serializedObject.FindProperty("stereoCameraEye");
            anchorTransformProp = serializedObject.FindProperty("anchorTransform");
            canvasOriginProp = serializedObject.FindProperty("canvasOrigin");
            shouldRenderProp = serializedObject.FindProperty("shouldRender");
            textureResProp = serializedObject.FindProperty("textureResolutionScale");
            ignoreWhenRenderProp = serializedObject.FindProperty("ignoreWhenRender");
            isMirrorProp = serializedObject.FindProperty("isMirror");
        }

        public override void OnInspectorGUI()
        {
            if (target == null || serializedObject == null) return;

            serializedObject.Update();

            // get target script
            var script = target as StereoRenderer;

            // tell editor to detect change in inspector GUI
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(stereoCameraHeadProp);
            EditorGUILayout.PropertyField(stereoCameraEyeProp);

            // create a horizontal line to seperate things
            EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

            // get anchor pose from specified transform or inspector 
            EditorGUILayout.PropertyField(canvasOriginProp);
            if (script.canvasOrigin == null)
            {
                script.canvasOriginPos = EditorGUILayout.Vector3Field("Canvas Origin World Pos", script.canvasOriginPos);
                script.canvasOriginEuler = EditorGUILayout.Vector3Field("Canvas Origin World Rot", script.canvasOriginEuler);
            }

            // create a horizontal line to seperate things
            EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

            // get anchor pose from specified transform or inspector 
            EditorGUILayout.PropertyField(anchorTransformProp);
            if (script.anchorTransform == null)
            {
                script.anchorPos = EditorGUILayout.Vector3Field("Anchor World Pos", script.anchorPos);
                script.anchorEuler = EditorGUILayout.Vector3Field("Anchor World Rot", script.anchorEuler);
            }

            // create a horizontal line to seperate things
            EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

            // get "shouldRender" flag value
            EditorGUILayout.PropertyField(shouldRenderProp);

            EditorGUILayout.PropertyField(textureResProp);

            // get a list of gameObjects that should be ignored during rendering
            EditorGUILayout.PropertyField(ignoreWhenRenderProp, true);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }

            // create a horizontal line to seperate things
            EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);

            // get "isMirror" flag value; if checked, automatically
            EditorGUILayout.PropertyField(isMirrorProp);
            if (script.isMirror)
            {
                script.anchorPos = script.canvasOriginPos;
                script.anchorRot = script.canvasOriginRot;
            }

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void OnSceneGUI()
        {
            if (target == null || serializedObject == null) return;

            var script = target as StereoRenderer;

            // draw handle for anchor object
            if (script.anchorTransform == null)
            {
                var anchorPos = script.anchorPos;
                var anchorRot = script.anchorRot;

                EditorGUI.BeginChangeCheck();
                var newAnchorPos = Handles.PositionHandle(anchorPos, anchorRot);
                var anchorPosChanged = EditorGUI.EndChangeCheck();

                EditorGUI.BeginChangeCheck();
                var newAnchorRot = Handles.RotationHandle(anchorRot, anchorPos);
                var anchorRotChanged = EditorGUI.EndChangeCheck();

                Handles.color = anchorColor;
                Handles.CubeCap(0, newAnchorPos, newAnchorRot, cubeSize);

                if (anchorPosChanged || anchorRotChanged)
                {
                    Undo.RecordObject(target, "Stereo Anchor Pose Changed");

                    if (anchorPosChanged) { script.anchorPos = newAnchorPos; }
                    if (anchorRotChanged) { script.anchorRot = newAnchorRot; }

                    EditorUtility.SetDirty(target);
                }
            }
            else
            {
                Handles.color = anchorColor;
                Handles.CubeCap(0, script.anchorTransform.transform.position, script.anchorTransform.transform.rotation, cubeSize);
            }

            // draw handle for canvas origin object
            if(script.canvasOrigin == null)
            {
                var canvasOriginPos = script.canvasOriginPos;
                var canvasOriginRot = script.canvasOriginRot;

                EditorGUI.BeginChangeCheck();
                var newOriginPos = Handles.PositionHandle(canvasOriginPos, canvasOriginRot);
                var originPosChanged = EditorGUI.EndChangeCheck();

                EditorGUI.BeginChangeCheck();
                var newOriginRot = Handles.RotationHandle(canvasOriginRot, canvasOriginPos);
                var originRotChanged = EditorGUI.EndChangeCheck();

                Handles.color = originColor;
                Handles.CubeCap(0, newOriginPos, newOriginRot, cubeSize);

                if (originPosChanged || originRotChanged)
                {
                    Undo.RecordObject(target, "Canvas Origin Pose Changed");

                    if (originPosChanged) { script.canvasOriginPos = newOriginPos; }
                    if (originRotChanged) { script.canvasOriginRot = newOriginRot; }

                    EditorUtility.SetDirty(target);
                }
            }
            else
            {
                Handles.color = originColor;
                Handles.CubeCap(0, script.canvasOrigin.transform.position, script.canvasOrigin.transform.rotation, cubeSize);
            }
        }
    }
}