using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class BonemapReader
{
    public static BVHDriver.BoneMap[] bonemaps;

    public static void Read(string filename)
    {
        List<BVHDriver.BoneMap> maplist = new List<BVHDriver.BoneMap>();
        using (StreamReader sr = new StreamReader(filename))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string[] words = line.Split(' ');
                if (words.Length != 2)
                {
                    Debug.Log("Bonesmap.txt format error");
                    continue;
                }
                BVHDriver.BoneMap tb = new BVHDriver.BoneMap();
                tb.bvh_name = words[0];
                tb.humanoid_bone = match(words[1]);
                maplist.Add(tb);
            }
        }
        bonemaps = maplist.ToArray();
    }

    public static HumanBodyBones match(string s)
    {
        switch(s)
        {
            case "Hips":
                return HumanBodyBones.Hips;
            case "Head":
                return HumanBodyBones.Head;
            case "LeftToes":
                return HumanBodyBones.LeftToes;
            case "RightToes":
                return HumanBodyBones.RightToes;
            case "RightUpperLeg":
                return HumanBodyBones.RightUpperLeg;
            case "RightLowerLeg":
                return HumanBodyBones.RightLowerLeg;
            case "RightFoot":
                return HumanBodyBones.RightFoot;
            case "LeftUpperLeg":
                return HumanBodyBones.LeftUpperLeg;
            case "LeftLowerLeg":
                return HumanBodyBones.LeftLowerLeg;
            case "LeftFoot":
                return HumanBodyBones.LeftFoot;
            case "Spine":
                return HumanBodyBones.Spine;
            case "Chest":
                return HumanBodyBones.Chest;
            case "UpperChest":
                return HumanBodyBones.UpperChest;
            case "Neck":
                return HumanBodyBones.Neck;
            case "RightShoulder":
                return HumanBodyBones.RightShoulder;
            case "RightUpperArm":
                return HumanBodyBones.RightUpperArm;
            case "RightLowerArm":
                return HumanBodyBones.RightLowerArm;
            case "RightHand":
                return HumanBodyBones.RightHand;
            case "LeftShoulder":
                return HumanBodyBones.LeftShoulder;
            case "LeftUpperArm":
                return HumanBodyBones.LeftUpperArm;
            case "LeftLowerArm":
                return HumanBodyBones.LeftLowerArm;
            case "LeftHand":
                return HumanBodyBones.LeftHand;
            case "RightThumbProximal":
                return HumanBodyBones.RightThumbProximal;
            case "RightThumbIntermediate":
                return HumanBodyBones.RightThumbIntermediate;
            case "RightThumbDistal":
                return HumanBodyBones.RightThumbDistal;
            case "RightIndexProximal":
                return HumanBodyBones.RightIndexProximal;
            case "RightIndexIntermediate":
                return HumanBodyBones.RightIndexIntermediate;
            case "RightIndexDistal":
                return HumanBodyBones.RightIndexDistal;
            case "RightMiddleProximal":
                return HumanBodyBones.RightMiddleProximal;
            case "RightMiddleIntermediate":
                return HumanBodyBones.RightMiddleIntermediate;
            case "RightMiddleDistal":
                return HumanBodyBones.RightMiddleDistal;
            case "RightRingProximal":
                return HumanBodyBones.RightRingProximal;
            case "RightRingIntermediate":
                return HumanBodyBones.RightRingIntermediate;
            case "RightRingDistal":
                return HumanBodyBones.RightRingDistal;
            case "RightLittleProximal":
                return HumanBodyBones.RightLittleProximal;
            case "RightLittleIntermediate":
                return HumanBodyBones.RightLittleIntermediate;
            case "RightLittleDistal":
                return HumanBodyBones.RightLittleDistal;
            case "LeftThumbProximal":
                return HumanBodyBones.LeftThumbProximal;
            case "LeftThumbIntermediate":
                return HumanBodyBones.LeftThumbIntermediate;
            case "LeftThumbDistal":
                return HumanBodyBones.LeftThumbDistal;
            case "LeftIndexProximal":
                return HumanBodyBones.LeftIndexProximal;
            case "LeftIndexIntermediate":
                return HumanBodyBones.LeftIndexIntermediate;
            case "LeftIndexDistal":
                return HumanBodyBones.LeftIndexDistal;
            case "LeftMiddleProximal":
                return HumanBodyBones.LeftMiddleProximal;
            case "LeftMiddleIntermediate":
                return HumanBodyBones.LeftMiddleIntermediate;
            case "LeftMiddleDistal":
                return HumanBodyBones.LeftMiddleDistal;
            case "LeftRingProximal":
                return HumanBodyBones.LeftRingProximal;
            case "LeftRingIntermediate":
                return HumanBodyBones.LeftRingIntermediate;
            case "LeftRingDistal":
                return HumanBodyBones.LeftRingDistal;
            case "LeftLittleProximal":
                return HumanBodyBones.LeftLittleProximal;
            case "LeftLittleIntermediate":
                return HumanBodyBones.LeftLittleIntermediate;
            case "LeftLittleDistal":
                return HumanBodyBones.LeftLittleDistal;
            default:
                Debug.Log("Bonesmap input not in HumanBodyBones");
                break;
        }
        
        return HumanBodyBones.Hips;
    }
}