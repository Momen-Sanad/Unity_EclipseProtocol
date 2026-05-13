using EclipseProtocol.AI;
using UnityEditor;
using UnityEngine;

namespace EclipseProtocol.EditorTools
{
    public static class PatrolDroneVisualPrefabBuilder
    {
        private const string ModelPath = "Assets/_project/Prefabs/Enemies/Patrol Drone/Military.fbx";
        private const string VisualPrefabPath = "Assets/_project/Prefabs/Enemies/PatrolDroneVisual.prefab";
        private const string PatrolDronePrefabPath = "Assets/_project/Prefabs/Enemies/PatrolDrone.prefab";

        [MenuItem("Tools/Eclipse Protocol/Rebuild Patrol Drone Visual Prefab")]
        public static void BuildAndAssign()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"Could not load patrol drone model at {ModelPath}.");
                return;
            }

            GameObject wrapper = new GameObject("PatrolDroneVisual");
            GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset, wrapper.transform) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = Object.Instantiate(modelAsset, wrapper.transform);
            }

            modelInstance.name = modelAsset.name;
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(wrapper);
                Debug.LogError($"Patrol drone model at {ModelPath} has no renderers.");
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            modelInstance.transform.position -= bounds.center;

            GameObject visualPrefab = PrefabUtility.SaveAsPrefabAsset(wrapper, VisualPrefabPath);
            Object.DestroyImmediate(wrapper);
            if (visualPrefab == null)
            {
                Debug.LogError($"Could not save patrol drone visual prefab at {VisualPrefabPath}.");
                return;
            }

            GameObject patrolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatrolDronePrefabPath);
            if (patrolPrefab == null)
            {
                Debug.LogError($"Could not load patrol drone prefab at {PatrolDronePrefabPath}.");
                return;
            }

            DronePatrolAI patrolAI = patrolPrefab.GetComponent<DronePatrolAI>();
            if (patrolAI == null)
            {
                Debug.LogError($"Patrol drone prefab at {PatrolDronePrefabPath} has no DronePatrolAI component.");
                return;
            }

            SerializedObject serializedPatrolAI = new SerializedObject(patrolAI);
            serializedPatrolAI.FindProperty("visualPrefab").objectReferenceValue = visualPrefab;
            serializedPatrolAI.FindProperty("visualLocalPosition").vector3Value = Vector3.zero;
            serializedPatrolAI.FindProperty("visualLocalEulerAngles").vector3Value = Vector3.zero;
            serializedPatrolAI.FindProperty("visualLocalScale").vector3Value = Vector3.one * 10f;
            serializedPatrolAI.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(patrolAI);
            EditorUtility.SetDirty(patrolPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Patrol drone visual assigned. Wrapper renderers: {renderers.Length}, bounds size: {bounds.size}.");
        }
    }
}
