using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EnvironmentConfiguration;
using UnityEngine;

public class RectangularGrid : MonoBehaviour
{
    private EnvSettings envSettings;
    private Dictionary<int, Vector3> centreToCoords = new();
    private Dictionary<Vector3, int> coordsToCentre = new();
    private Dictionary<int, List<int>> neighbourhood = new();
    private List<List<int>> neighbors = new();
    private MeshCollider meshCollider;

    public int maxCellIndex;

    // Start is called before the first frame update
    void Start()
    {
        Clear();
        envSettings = this.GetComponentInParent<EnvSettings>();
        meshCollider = this.GetComponent<MeshCollider>();
        var planeCentre = this.transform.localPosition;
        Vector3 planeExtents = meshCollider.bounds.extents;

        int cellIndex = 0;
        var gridCellSize = this.envSettings.gridCellSize;

        Vector3 startPoint = planeCentre - planeExtents + gridCellSize / 2;
        AddCell(cellIndex, startPoint);

        while (true) 
        {
            var tmpPoint = startPoint;
            List<int> tmpNeighbourList = new();
            while (true)
            {
                tmpNeighbourList.Add(cellIndex);
                tmpPoint += Vector3.Scale(gridCellSize, Vector3.right);
                ++cellIndex;

                if (!meshCollider.bounds.Contains(tmpPoint))
                {
                    break;
                }

                AddCell(cellIndex, tmpPoint);
            }

            startPoint += Vector3.Scale(gridCellSize, Vector3.forward);

            if (!meshCollider.bounds.Contains(startPoint))
            {
                break;
            }
            ++cellIndex;
            AddCell(cellIndex, startPoint);
            neighbors.Add(tmpNeighbourList);
        }

        maxCellIndex = cellIndex;

        //filling the neighbourhoods
        for (int i = 0; i < neighbors.Count; ++i)
        {
            for (int j = 0; j < neighbors[i].Count; ++j)
            {
                if (i + 1 > neighbors.Count)
                {
                    neighbourhood[neighbors[i][j]].Add(neighbors[i][j]);
                } 
                else neighbourhood[neighbors[i][j]].Add(neighbors[i+1][j]);

                if (i - 1 < 0)
                {
                    neighbourhood[neighbors[i][j]].Add(neighbors[i][j]);
                }
                else neighbourhood[neighbors[i][j]].Add(neighbors[i - 1][j]);

                if (i + 1 > neighbors.Count)
                {
                    neighbourhood[neighbors[i][j]].Add(neighbors[i][j]);
                }
                else neighbourhood[neighbors[i][j]].Add(neighbors[i + 1][j]);

                if (i - 1 < 0)
                {
                    neighbourhood[neighbors[i][j]].Add(neighbors[i][j]);
                }
                else neighbourhood[neighbors[i][j]].Add(neighbors[i - 1][j]);

                if (j - 1 < 0)
                {
                    neighbourhood[neighbors[i][j]].Add(neighbors[i][j]);
                }
                else neighbourhood[neighbors[i][j]].Add(neighbors[i][j - 1]);

                if (j + 1 > neighbors[i].Count)
                {
                    neighbourhood[neighbors[i][j]].Add(neighbors[i][j]);
                }
                else neighbourhood[neighbors[i][j]].Add(neighbors[i][j+1]);
            }
        }

    }

    public bool IsCoordInGrid(Vector3 coord)
    {
        return coordsToCentre.ContainsKey(coord);
    }

    public int GetCellByCoords(Vector3 coord)
    {
        return coordsToCentre[coord];
    }

    public Vector3 GetCoords(int centre)
    {
        return centreToCoords[centre];
    }

    public List<int> GetNeighbours(int centre) 
    {
        return neighbourhood[centre];
    }

    public void AddCell(int centre, Vector3 coord) 
    {
        
        centreToCoords[centre] = coord;
        coordsToCentre[coord] = centre;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = UnityEngine.Color.black;
        foreach (var item in centreToCoords.Values)
        {
            Gizmos.DrawSphere(item, 3);
        }
    }

    public void Clear()
    {
        centreToCoords.Clear();
        coordsToCentre.Clear();
        neighbourhood.Clear();
    }

    // Update is called once per frame
    void Update()
    {
        //this method could be needed if only the grid 
        //is procedurally generated
    }
}
