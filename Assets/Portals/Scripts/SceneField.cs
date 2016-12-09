using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Thanks to glitchers and Halfbiscuit @ http://answers.unity3d.com/questions/242794/inspector-field-for-scene-asset.html

namespace Portals {
    [System.Serializable]
    public class SceneField {
        [SerializeField]
        private Object _sceneAsset;
        [SerializeField]
        private string _sceneName = "";
        public string name {
            get { return _sceneName; }
        }
        // makes it work with the existing Unity methods (LoadLevel/LoadScene)
        public static implicit operator string(SceneField sceneField) {
            return sceneField.name;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SceneField))]
    public class SceneFieldPropertyDrawer : PropertyDrawer {
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            EditorGUI.BeginProperty(_position, GUIContent.none, _property);
            SerializedProperty sceneAsset = _property.FindPropertyRelative("_sceneAsset");
            SerializedProperty sceneName = _property.FindPropertyRelative("_sceneName");
            _position = EditorGUI.PrefixLabel(_position, GUIUtility.GetControlID(FocusType.Passive), _label);
            if (sceneAsset != null) {
                EditorGUI.BeginChangeCheck();

                Object value = EditorGUI.ObjectField(_position, sceneAsset.objectReferenceValue, typeof(SceneAsset), false);
                if (EditorGUI.EndChangeCheck()) {
                    sceneAsset.objectReferenceValue = value;
                    if (sceneAsset.objectReferenceValue != null) {
                        sceneName.stringValue = (sceneAsset.objectReferenceValue as SceneAsset).name;
                    }
                }
            }
            EditorGUI.EndProperty();
        }
    }
#endif
}