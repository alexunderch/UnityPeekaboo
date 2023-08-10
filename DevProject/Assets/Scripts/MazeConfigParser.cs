using System;
using System.Collections.Generic;
using System.IO;

/*
Here we define configuration files workflow
Any environment could be expressed as a json configutaion with the following nested structure 
"Agents":
{
    Name:
    {
        "Position": Vector3
        "Rotation": Quaternion
        "Type": String
    }, ...    
},
"Goals":
{
    Name: 
    {
        "Position": Vector3
        "Rotation": Quaternion (if needed)
        "Type": String
    }, ...
},
"Map":
{
    "Size": Vector2,
    "BaseBlockScale": Vector3,
    "Walls":
    {
        Name: 
        {
            "Position": Vector3
            "Rotation": Quaternion 
            "Type": String
        }, ...
    }
}
*/

namespace MazeConfiguration 
{
    using UnityEngine;
    
    [System.Serializable]
    public class SerializableBuildingBlock
    {
        public float[] Position;
        //only around Yth axis actually
        public float[] Rotation;
        public string Type;

        public SerializableBuildingBlock(Vector3 position, Quaternion rotation, string type)
        {
            Position = ToArray(position);
            Rotation = ToArray(rotation);
            Type = type;

        }   

        public static float[] ToArray(Vector3 v)
        {
            return new[] {v.x, v.y, v.z};

        }  

        public static int[] ToArray(Vector2 v)
        {
            return new[] {(int)v.x, (int)v.y};

        }    

        public static float[] ToArray(Quaternion q)
        {
            return new[] {q.x, q.y, q.z, q.w};
        } 
    }


    public class NonSerializableBuildingBlock
    {
        public Vector3 Position;
        //only around Yth axis actually
        public Quaternion Rotation;
        public string Type;

        public NonSerializableBuildingBlock(float[] position, float[] rotation, string type)
        {
            //json deserealization
            Position = new Vector3(position[0], position[1], position[2]);
            Rotation = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
            Type = type;

        }   
        public NonSerializableBuildingBlock(Vector3 position, Quaternion rotation, string type)
        {
            //unity
            Position = position;
            Rotation = rotation;
            Type = type;

        }   


        public SerializableBuildingBlock Serialize()
        {
            return new SerializableBuildingBlock 
            (
                this.Position,
                this.Rotation,
                this.Type
            );
        }
    }

    
    [System.Serializable]
    public class MapStructureDescription 
    {
        public int[] mapSize;
        public float[] baseBuildingBlockSize;
        public List<SerializableBuildingBlock> Walls;

        public MapStructureDescription
        (
            int[] cmapSize, 
            float[] cbaseBuildingBlockSize, 
            List<SerializableBuildingBlock> walls
        )
        {
            mapSize = cmapSize;
            baseBuildingBlockSize = cbaseBuildingBlockSize;
            Walls = walls;
        }
    };

    [System.Serializable]
    public class ConfigStructureDecription
    {
    
        public List<SerializableBuildingBlock> Agents;
        public List<SerializableBuildingBlock> Goals;
        public MapStructureDescription Map;

        public ConfigStructureDecription
        (
            List<SerializableBuildingBlock> agents, 
            List<SerializableBuildingBlock> goals, 
            MapStructureDescription map
        )
        {
            Agents = agents;
            Goals = goals;
            Map = map;
        }
    }

    class MazeBuilder
    {   
        //h, w
        private Vector2 mapSize;
        //x, y, z = width, height, depth
        private Vector3 baseBlockSize;

        public List<NonSerializableBuildingBlock> Room = new List<NonSerializableBuildingBlock>();
        public List<NonSerializableBuildingBlock> Agents = new List<NonSerializableBuildingBlock>();
        public List<NonSerializableBuildingBlock> Goals = new List<NonSerializableBuildingBlock>();
        

        public void SetMapSizes(Vector2 globalMapSize, Vector3 buildingBlockSize)
        {
            //for reading from unity
            mapSize = globalMapSize;
            baseBlockSize = buildingBlockSize;
        }

        public void SetMapSizes(int[] globalMapSize, float[] buildingBlockSize)
        {
            //for reading from json
            mapSize = new Vector2((float)globalMapSize[0], (float)globalMapSize[1]);
            baseBlockSize = new Vector3(buildingBlockSize[0], buildingBlockSize[1], buildingBlockSize[2]);
        }


        public void Clear()
        {
            Room.Clear();
            Agents.Clear();
            Goals.Clear();
        }        
        
        public List<SerializableBuildingBlock> SerializeList(List<NonSerializableBuildingBlock> list)
        {
            var serializedList = new List<SerializableBuildingBlock>();

            foreach(var item in list)
            {
                serializedList.Add(item.Serialize());
            }
            return serializedList;
        }
        public void LoadMaze(string sourceFilename)
        {
            //fully overwrites a current maze data!
            var jsonString = File.ReadAllText(sourceFilename);
            var config = JsonUtility.FromJson<ConfigStructureDecription>(jsonString);

            var map = config.Map;

            Clear();
            SetMapSizes(map.mapSize, map.baseBuildingBlockSize);


            foreach (var item in map.Walls)
            {
                Room.Add
                (
                    new NonSerializableBuildingBlock(item.Position, item.Rotation, item.Type)
                );
            }

            foreach (var item in config.Agents)
            {
                Agents.Add
                (
                    new NonSerializableBuildingBlock(item.Position, item.Rotation, item.Type)
                );
            }

            foreach (var item in config.Goals)
            {
                Goals.Add
                (
                    new NonSerializableBuildingBlock(item.Position, item.Rotation, item.Type)
                );
            }
        }

        public void DumpMaze(string targetFilename)
        {
            var map = new MapStructureDescription
            (
                SerializableBuildingBlock.ToArray(mapSize),
                SerializableBuildingBlock.ToArray(baseBlockSize),
                this.SerializeList(Room)
            );

            var config = new ConfigStructureDecription
            (
                this.SerializeList(Agents),
                this.SerializeList(Goals),
                map
            );
            string jsonString = JsonUtility.ToJson(config, true);
            File.WriteAllText(targetFilename, jsonString);
        }

    }


}