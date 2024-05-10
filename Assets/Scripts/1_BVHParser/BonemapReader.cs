using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class BonemapReader
{
    public static BVHDriver.BoneMap[] bonemaps;

    /********************************************************************
     * BoneReader.Read: 将BVH文件中的骨骼动作数据映射到Unity中的人体骨骼上
     * 最终解析的格式是一个BVH骨骼名称与Unity人体骨骼名称的映射列表
     * 每个映射关系包含两个字段：BVH中的骨骼名称(bvh_name)和对应的Unity中的人体骨骼名称(humanoid_bone)
     * *************************************************************************************************/
    public static void Read(string filename)
    {
        // 创建一个名为maplist的列表，用于存储骨骼映射关系的数据。
        List<BVHDriver.BoneMap> maplist = new List<BVHDriver.BoneMap>();
        // 使用StreamReader类打开指定的BVH文件，filename是传入的参数，表示BVH文件的路径
        using (StreamReader sr = new StreamReader(filename))
        {
            string line;        // 声明一个字符串变量line，用于存储从文件中读取的每一行数据
            while ((line = sr.ReadLine()) != null)      // 循环读取文件的每一行，直到文件末尾
            {
                string[] words = line.Split(' ');       // 将每一行以空格为分隔符拆分成单词数组
                // 检查拆分后的数组长度是否为2，如果不是，说明该行数据不符合预期的格式，输出错误信息并继续下一行的解析
                if (words.Length != 2)
                {
                    Debug.Log("Bonesmap.txt format error");
                    continue;
                }
                /*********************************************
                 * 创建一个BoneMap对象，用于存储骨骼映射信息
                 * bvh_name属性：      BVH文件中的骨骼名称，即拆分后的第一个单词
                 * humanoid_bone属性： 调用match方法将拆分后的第二个单词转换为骨骼关节惯用名称所对应的unity内置骨骼名称
                 * ************************************************/
                BVHDriver.BoneMap tb = new BVHDriver.BoneMap();
                tb.bvh_name = words[0];
                tb.humanoid_bone = match(words[1]);
                maplist.Add(tb);
            }
        }
        bonemaps = maplist.ToArray();
    }

    /**********************************************************************************
     * 定义用于retargetting的Bonemap，指定动捕资源与unity内置人体骨骼关节的对应关系：
     * 1. 在Bonemap文件夹中新建txt文件
     * 2. BVH动捕文件中的骨骼名称 unity内置骨骼关节常量名称所对应的骨骼惯用名称
    ***************************************************************************/

    // 枚举类型HumanBodyBones：用于表示Unity内置人体骨骼的名称，每个名称常量对应Humanoid中的一个骨骼
    // 下面的match函数将Unity内置人体骨骼常量与骨骼惯用名称关联起来，以便用于Bonemaps中的骨骼关节重绑定
    public static HumanBodyBones match(string s)
    {
        switch(s)
        {
            case "Hips":
                return HumanBodyBones.Hips;
            case "Head":
                return HumanBodyBones.Head;
            case "LeftToeBase":
                return HumanBodyBones.LeftToes;
            case "RightToeBase":
                return HumanBodyBones.RightToes;
            case "RightUpLeg":
                return HumanBodyBones.RightUpperLeg;
            case "RightLeg":
                return HumanBodyBones.RightLowerLeg;
            case "RightFoot":
                return HumanBodyBones.RightFoot;
            case "LeftUpLeg":
                return HumanBodyBones.LeftUpperLeg;
            case "LeftLeg":
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
            case "RightArm":
                return HumanBodyBones.RightUpperArm;
            case "RightForeArm":
                return HumanBodyBones.RightLowerArm;
            case "RightHand":
                return HumanBodyBones.RightHand;
            case "LeftShoulder":
                return HumanBodyBones.LeftShoulder;
            case "LeftArm":
                return HumanBodyBones.LeftUpperArm;
            case "LeftForeArm":
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