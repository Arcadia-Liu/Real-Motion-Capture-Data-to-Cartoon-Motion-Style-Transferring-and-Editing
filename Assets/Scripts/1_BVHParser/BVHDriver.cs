using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts;

// TODO: 调整frame
// TODO: 目前只缩放了左腿骨骼的长度

public class BVHDriver : MonoBehaviour
{
    [Header("Loader settings")]
    [Tooltip("This is the target avatar for which the animation should be loaded. Bone names should be identical to those in the BVH file and unique. All bones should be initialized with zero rotations. This is usually the case for VRM avatars.")]
    public Animator targetAvatar;

    [Tooltip("This is the path to the file which describes Bonemaps between unity and bvh.")]
    public string bonemapPath = @"Assets/Scripts/0_BoneMaps/Bonemaps_CMU.txt";
    [Tooltip("This is the path to the BVH file that should be loaded. Bone offsets are currently being ignored by this loader.")]
    public string filename;
    [Tooltip("If the flag above is disabled, the frame rate given in the BVH file will be overridden by this value.")]
    public float frameRate = 24.0f;
    public float nearRatio = 30000f;
    public GameObject modelMesh;

    [Serializable]
    public struct BoneMap
    {
        public string bvh_name;
        public HumanBodyBones humanoid_bone;
    }
    public BoneMap[] bonemaps; // the corresponding bones between unity and bvh
    public GameObject startPos;
    public Camera currentCamera;

    private BVHParser bp = null;
    private Animator anim;

    // This function doesn't call any Unity API functions and should be safe to call from another thread
    public void parseFile()
    {
        string bvhData = File.ReadAllText(filename);
        bp = new BVHParser(bvhData);    
        frameRate = 1f / bp.frameTime;
    }

    private Dictionary<string, Quaternion> bvhT;
    private Dictionary<string, Vector3> bvhOffset;
    private Dictionary<string, string> bvhHireachy;
    private Dictionary<HumanBodyBones, Quaternion> unityT;

    private int frameIdx;
    private float scaleRatio = 0.0f;

    private void Start()
    {
        
        // set mapping between bvh_name and humanBodyBones
        // 调用BoneReader.Read，解析BVH文件
        BonemapReader.Read(bonemapPath);
        // 设置 bvh_name 和 humanBodyBones 之间的映射关系
        bonemaps = BonemapReader.bonemaps;
        
        parseFile();
        Application.targetFrameRate = (Int16)frameRate;

        // 获取bvh文件第0帧位置的BoneData，即bvh骨骼的Tpose信息
        bvhT = bp.getKeyFrame(0);
        bvhOffset = bp.getOffset(1.0f);
        // 获得bvh文件的骨架结构信息，包含骨骼名称和骨骼节点的父子关系
        bvhHireachy = bp.getHierachy();
        // 获取目标角色模型的Animator组件，用于后续对角色动画的控制
        anim = targetAvatar.GetComponent<Animator>();
        unityT = new Dictionary<HumanBodyBones, Quaternion>();
        // 遍历骨骼映射数据
        foreach (BoneMap bm in bonemaps)
        {
            // 将Tpose下，unity对象内置骨骼的名称和相对世界坐标系的旋转信息记录在unityT中
            unityT.Add(bm.humanoid_bone, anim.GetBoneTransform(bm.humanoid_bone).rotation);
        }
        
        // 计算unity模型左腿的长度
        float unity_leftleg = (float)Math.Sqrt((anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position - anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position).sqrMagnitude) +
                              (float)Math.Sqrt((anim.GetBoneTransform(HumanBodyBones.LeftFoot).position - anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position).sqrMagnitude);
        // 计算bvh左腿的长度
        float bvh_leftleg = 0.0f;
        foreach(BoneMap bm in bonemaps) {
            if(bm.humanoid_bone==HumanBodyBones.LeftLowerLeg || bm.humanoid_bone == HumanBodyBones.LeftFoot)
            {
                bvh_leftleg = bvh_leftleg + (float)Math.Sqrt(bvhOffset[bm.bvh_name].sqrMagnitude);
            }
        }
        // 计算骨骼缩放
        scaleRatio = unity_leftleg / bvh_leftleg;
        frameIdx = 1;

        
    }

    private void Update()
    {
        // 得到当前帧骨骼数据boneData
        Dictionary<string, Quaternion> currFrame = bp.getKeyFrame(frameIdx);    // frameIdx 2871
        Dictionary<string, Quaternion> lastFrame = bp.getKeyFrame(frameIdx - 1);
        

        // 根据bvh中骨骼关节点的local rotation，更新unity中的animation骨骼相对世界坐标系的旋转rotation
        BVHLocalRotationToUnityRotation(currFrame, bonemaps, bvhT, unityT);
        Dictionary<string, Vector3> currBVHPos = getBVHPos(currFrame, bvhHireachy, bvhOffset);
        Dictionary<string, Vector3> lastBVHPos = getBVHPos(lastFrame, bvhHireachy, bvhOffset);
        // BVHPosToBVHMove(currBVHPos);



        // 将BVH中的物体位移转换为unity 2D 运动规范
        BVHPosToUnityMovement(currBVHPos, lastBVHPos, frameIdx);


        // 动画帧累加和循环播放
        if (frameIdx < bp.frames - 1)
        {
            frameIdx++;
        }
        else
        {
            frameIdx = 1;
        }

    }

    // 返回各个关节节点在世界空间中的位置
    private Dictionary<string, Vector3> getBVHPos(Dictionary<string, Quaternion> currFrame, 
        Dictionary<string, string> bvhHireachy, Dictionary<string, Vector3> bvhOffset)
    {
        Dictionary<string, Vector3> bvhPos = new Dictionary<string, Vector3>();
        foreach (string bname in currFrame.Keys)
        {
            // 如果当前骨骼为根骨骼，则将根骨骼位置直接添加到bvhPos
            if (bname == "pos")
            {
                bvhPos.Add(bp.root.name, new Vector3(currFrame["pos"].x, currFrame["pos"].y, currFrame["pos"].z));
            }
            else
            {
                // 如果当前骨骼存在于BVH骨骼层次结构中，且不是根骨骼
                // 通过父骨骼的位置、父骨骼的旋转和当前骨骼的偏移量来计算当前骨骼的位置，并记录在bvhPos
                if (bvhHireachy.ContainsKey(bname) && bname != bp.root.name)
                {
                    Vector3 curpos = bvhPos[bvhHireachy[bname]] + currFrame[bvhHireachy[bname]] * bvhOffset[bname];
                    bvhPos.Add(bname, curpos);
                }
            }
        }
        return bvhPos;  
    }

    private void BVHPosToUnityMovement(Dictionary<string, Vector3> currBVHPos, Dictionary<string, Vector3> lastBVHPos, int frameIndex)
    {
        // moveDirection:{ Forward2D: 2D角色前后方向，Up2D: 2D角色上下方向，Near2D: 2D角色z轴深度方向}
        Dictionary<string, Vector3> moveDirection = get2DWorldDirection();
        // 得到character自身的三维方向
        Dictionary<string, Vector3> characterDirection = get3DCharacterDirection(); 
        // 计算根节点的平均位移速度
        Vector3 currVelocity = getAverageVelocity(currBVHPos, lastBVHPos);
        // 将速度投影到character自身的三维方向
        Vector3 forwardVelocity = Vector3.Project(currVelocity, characterDirection["Forward3D"]);
        Vector3 upVelocity = Vector3.Project(currVelocity, characterDirection["Up3D"]);
        Vector3 nearVelocity = Vector3.Project(currVelocity, characterDirection["Near3D"]);
        // Debug: 绘制当前位移速度方向
        Debug.DrawRay(anim.GetBoneTransform(HumanBodyBones.Hips).position, forwardVelocity * 1000f, Color.red);
        Debug.DrawRay(anim.GetBoneTransform(HumanBodyBones.Hips).position, upVelocity * 1000f, Color.blue);
        Debug.DrawRay(anim.GetBoneTransform(HumanBodyBones.Hips).position, nearVelocity * 1000f, Color.green);
        // 根据帧和速度在特定方向上更新根节点位移
        anim.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.Dot(forwardVelocity.normalized, moveDirection["Forward2D"].normalized) * forwardVelocity.magnitude * moveDirection["Forward2D"] * bp.frameTime;
        anim.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.Dot(upVelocity.normalized, moveDirection["Up2D"].normalized) * upVelocity.magnitude * moveDirection["Up2D"] * bp.frameTime;

        // 使用投影Near2D方向的方法更新深度影响的scale
        // 用相机矩阵计算nearRatio，然后调整加减正负的大小问题
        Vector3 characterHip = anim.GetBoneTransform(HumanBodyBones.Hips).transform.position;
        nearRatio = Camera_GetVerticalDistance_Character(characterHip);
        float delta_s = Vector3.Dot(nearVelocity.normalized, moveDirection["Near2D"].normalized) * nearVelocity.magnitude * bp.frameTime;
        float nearScale = Vector3.Dot(nearVelocity.normalized, moveDirection["Near2D"].normalized) * (nearRatio / nearRatio + delta_s);
        float scaleSpeed = 0.0005f;
        Vector3 newScale = new Vector3(gameObject.transform.localScale.x * nearScale, gameObject.transform.localScale.y * nearScale, gameObject.transform.localScale.z * nearScale);
        gameObject.transform.localScale += newScale * scaleSpeed;

        // 循环播放动画的需要
        if (frameIndex == 1)
        {
            anim.GetBoneTransform(HumanBodyBones.Hips).position = startPos.transform.position;
            gameObject.transform.localScale = startPos.transform.localScale;
        }
    }

    private float Camera_GetVerticalDistance_Character(Vector3 characterHip)
    {
        currentCamera = UnityEngine.Camera.main;
        Plane cameraPlane = new Plane(currentCamera.transform.forward, currentCamera.transform.position);
        float distance = cameraPlane.GetDistanceToPoint(characterHip);
        return distance;
    }

    private void BVHPosToBVHMove(Dictionary<string, Vector3> bvhPos)
    {
        // 更新角色位置属性，将BVH文件中根骨骼的位置信息（经过缩放）赋值给Unity模型中对应骨骼的位置属性
        anim.GetBoneTransform(HumanBodyBones.Hips).position = bvhPos[bp.root.name] * scaleRatio;

        // draw bvh skeleton
        foreach (string bname in bvhHireachy.Keys)
        {
            Color color = new Color(1.0f, 0.0f, 0.0f);
            Debug.DrawLine(bvhPos[bname] * scaleRatio, bvhPos[bvhHireachy[bname]] * scaleRatio, color);
        }
    }

    // 根据bvh中骨骼关节点的local rotation，更新unity中的animation骨骼相对世界坐标系的旋转rotation
    private void BVHLocalRotationToUnityRotation(Dictionary<string, Quaternion> currFrame, BoneMap[] bonemaps, 
        Dictionary<string, Quaternion> bvhT, Dictionary<HumanBodyBones, Quaternion> unityT)
    {
        foreach (BoneMap bm in bonemaps)
        {
            // 得到当前骨骼关节节点的Transform，包括各种世界关系和局部关系
            Transform currBone = anim.GetBoneTransform(bm.humanoid_bone);
            // 通过当前骨骼的local rotation，计算得到其相对世界坐标系的旋转rotation，并更新currBone，更新骨骼的transform参数
            currBone.rotation = (currFrame[bm.bvh_name] * Quaternion.Inverse(bvhT[bm.bvh_name])) * unityT[bm.humanoid_bone];
        }
    }

    private Dictionary<string, Vector3> get2DWorldDirection()
    {
        currentCamera = UnityEngine.Camera.main;
        Dictionary<string, Vector3> moveDirection = new Dictionary<string, Vector3>();
        moveDirection.Add("Forward2D", currentCamera.transform.right);
        moveDirection.Add("Up2D", currentCamera.transform.up);
        moveDirection.Add("Near2D", -currentCamera.transform.forward);
        return moveDirection;
    }

    private Dictionary<string, Vector3> get3DCharacterDirection()
    {
        currentCamera = UnityEngine.Camera.main;
        Dictionary<string, Vector3> characterDirection = new Dictionary<string, Vector3>();
        //characterDirection.Add("Forward3D", Vector3.right);
        characterDirection.Add("Forward3D", anim.GetBoneTransform(HumanBodyBones.Hips).transform.forward);
        // characterDirection.Add("Up3D", anim.GetBoneTransform(HumanBodyBones.Hips).transform.up);
        characterDirection.Add("Up3D", Vector3.up);
        if (Vector3.Dot(currentCamera.transform.forward, anim.GetBoneTransform(HumanBodyBones.Hips).transform.right) < 0)
        {
            characterDirection.Add("Near3D", anim.GetBoneTransform(HumanBodyBones.Hips).transform.right);
        }
        else
        {
            characterDirection.Add("Near3D", -anim.GetBoneTransform(HumanBodyBones.Hips).transform.right);
        }
        return characterDirection;
    }

    private Vector3 getAverageVelocity(Dictionary<string, Vector3> currBVHPos, Dictionary<string, Vector3> lastBVHPos)
    {
        Vector3 averageVelocity = (currBVHPos[bp.root.name] - lastBVHPos[bp.root.name]) * scaleRatio / bp.frameTime;
        return averageVelocity;
    }
}
