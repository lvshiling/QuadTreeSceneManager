﻿using UnityEngine;
using System.Collections.Generic;

public class QuadTreeSceneManager : MonoBehaviour
{
    public class SceneNode : IQuadTreeObject
    {
        public GameObject m_gameObject;
        public string m_name;
        public string m_parent;        
        public Vector3 m_vPosition;
        public bool m_bLoaded;

        public SceneNode(string parent, string name, Vector3 position, bool loaded = false)
        {
            m_parent = parent;
            m_name = name;
            m_vPosition = position; m_vPosition.y = 0;
            m_bLoaded = loaded;
        }

        public SceneNode(GameObject go, Vector3 position, bool loaded = false)
        {
            m_gameObject = go;
            m_vPosition = position; m_vPosition.y = 0;
            m_bLoaded = loaded;
        }

        public GameObject GetGameObject()
        {
            return m_gameObject;
        }

        public Vector2 GetPosition()
        {
            //Ignore the Y position, Quad-trees operate on a 2D plane.
            return new Vector2(m_vPosition.x, m_vPosition.z);
        }
    }

    public GameObject player;
    public int loadDistance = 20;
    public int unloadDistance = 30;

    public int mapSize = 256;
    public int mapDensity = 5;
    public int mapMinWidth = 10;

    QuadTree<SceneNode> quadTree;

    List<SceneNode> sceneNodes;

    Rect visibleArea;
    List<SceneNode> visibleSceneNodes;

    List<SceneNode> loadedSceneNodes = new List<SceneNode>();

    Dictionary<string, GameObject> subParent = new Dictionary<string, GameObject>();

    void OnEnable()
    {
        sceneNodes = RetrieveSceneNodes();

        quadTree = new QuadTree<SceneNode>(mapDensity, mapMinWidth, new Rect(-mapSize/2, -mapSize/2, mapSize, mapSize));
        foreach (SceneNode to in sceneNodes)
        {
            quadTree.Insert(to);
        }
    }

    void Awake()
    {
        Application.backgroundLoadingPriority = ThreadPriority.Normal;
    }

    void Update()
    {
        UpdateSceneNodes();
    }

    void OnDrawGizmos()
    {
        if (sceneNodes != null)
        {
            Gizmos.color = Color.white;
            foreach (SceneNode to in sceneNodes)
            {
                Gizmos.DrawSphere(to.m_vPosition, 1*mapSize/1024f);
            }
        }

        if (visibleArea != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawLine(new Vector3(visibleArea.x, 0, visibleArea.y), new Vector3(visibleArea.x, 0, visibleArea.y + visibleArea.height));
            Gizmos.DrawLine(new Vector3(visibleArea.x, 0, visibleArea.y), new Vector3(visibleArea.x + visibleArea.width, 0, visibleArea.y));
            Gizmos.DrawLine(new Vector3(visibleArea.x + visibleArea.width, 0, visibleArea.y), new Vector3(visibleArea.x + visibleArea.width, 0, visibleArea.y + visibleArea.height));
            Gizmos.DrawLine(new Vector3(visibleArea.x, 0, visibleArea.y + visibleArea.height), new Vector3(visibleArea.x + visibleArea.width, 0, visibleArea.y + visibleArea.height));
        }

        if (visibleSceneNodes != null)
        {
            Gizmos.color = Color.red;
            foreach (SceneNode to in visibleSceneNodes)
            {
                Gizmos.DrawSphere(to.m_vPosition, 1 * mapSize / 1024f);
            }
        }

        if (quadTree != null)
        {
            Gizmos.color = Color.black;
            quadTree.DrawDebug();
        }
    }

    List<SceneNode> RetrieveSceneNodes()
    {
        if (sceneNodes == null)
        {
            sceneNodes = new List<SceneNode>(100);
        }

        subParent.Clear();

        foreach (Transform t in transform)
        {
            SceneNodeData snd = t.GetComponent<SceneNodeData>();
            if (snd)
            {
                subParent[t.name] = t.gameObject;

                foreach(SceneNodeData.SceneNode sn in snd.sceneNodes)
                {
                    SceneNode newObject = new SceneNode(sn.parent, sn.name, sn.position);
                    sceneNodes.Add(newObject);
                }
            }
        }

        return sceneNodes;

        /*
        MeshRenderer[] mrs = GameObject.FindObjectsOfType<MeshRenderer>();
        SkinnedMeshRenderer[] smrs = GameObject.FindObjectsOfType<SkinnedMeshRenderer>();

        List<SceneNode> testObjects = new List<SceneNode>(mrs.Length);
        foreach (MeshRenderer mr in mrs)
        {
            if (mr.gameObject.layer == 8) // Road
                continue;

            SceneNode newObject = new SceneNode(mr.gameObject, new Vector3(mr.transform.position.x, 0, mr.transform.position.z));
            testObjects.Add(newObject);
        }

        foreach (SkinnedMeshRenderer smr in smrs)
        {
            if (smr.gameObject.layer == 8) // Road
                continue;

            SceneNode newObject = new SceneNode(smr.gameObject, new Vector3(smr.transform.position.x, 0, smr.transform.position.z));
            testObjects.Add(newObject);
        }

        return testObjects;
        */
    }

    Rect GetPlayerVisibleArea()
    {
        if (player != null)
        {
            Vector2 position = new Vector2(player.transform.position.x, player.transform.position.z);
            return new Rect(position.x - loadDistance, position.y - loadDistance, loadDistance*2, loadDistance*2);
        }

        return new Rect(Random.Range(-mapSize / 2, mapSize / 2), Random.Range(-mapSize / 2, mapSize / 2), 100 * mapSize / 1024f, 100 * mapSize / 1024f);
    }

    void UpdateSceneNodes()
    {
        visibleArea = GetPlayerVisibleArea();
        visibleSceneNodes = quadTree.RetrieveObjectsInArea(visibleArea);

        // load
        foreach (SceneNode sn in visibleSceneNodes)
        {
            if (!loadedSceneNodes.Contains(sn))
            {
                loadedSceneNodes.Add(sn);

                LoadSceneNode(sn);
            }
        }

        // unload
        List<SceneNode> needRemoveSceneNodes = new List<SceneNode>();
        if (player != null)
        {
            foreach (SceneNode sn in loadedSceneNodes)
            {
                if (sn.m_gameObject == null)
                    continue;

                Vector2 v = new Vector2(player.transform.position.x - sn.m_gameObject.transform.position.x, player.transform.position.z - sn.m_gameObject.transform.position.z);
                if (v.sqrMagnitude > unloadDistance * unloadDistance)
                {
                    needRemoveSceneNodes.Add(sn);                    
                }
            }

            foreach(SceneNode sn in needRemoveSceneNodes)
            {
                loadedSceneNodes.Remove(sn);

                UnloadSceneNode(sn);
            }
        }
    }

    void LoadSceneNode(SceneNode sn)
    {
        if (sn.m_bLoaded)
            return;

        UnityEngine.Object obj =  Resources.Load("st_jhs_01/" + sn.m_parent + "/" +sn.m_name);
        if (obj == null)
        {
            Debug.LogError(sn.m_name + "isn't exist!");
            return;
        }
        
        GameObject go = GameObject.Instantiate(obj) as GameObject;
        GameObject parent = subParent[sn.m_parent];
        if (parent)
        {
            go.transform.parent = parent.transform;
        } 

        sn.m_gameObject = go;
        sn.m_bLoaded = true;

        if (sn.m_gameObject)
        {
            sn.m_gameObject.SetActive(true);
        }
    }

    void UnloadSceneNode(SceneNode sn)
    {
        Destroy(sn.m_gameObject);
        sn.m_gameObject = null;
        sn.m_bLoaded = false;

        if (sn.m_gameObject)
        {
            sn.m_gameObject.SetActive(false);
        }
    }

    
}