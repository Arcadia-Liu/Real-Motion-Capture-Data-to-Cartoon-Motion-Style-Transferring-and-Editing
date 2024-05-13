using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts;
using JetBrains.Annotations;

public class BVHDriver : MonoBehaviour
{
    /***************************  Inspector面板UI public 可控变量  *******************************/
    [Header("Loader settings")]
    [Tooltip("This is the target avatar for which the animation should be loaded. Bone names should be identical to those in the BVH file and unique. All bones should be initialized with zero rotations. This is usually the case for VRM avatars.")]
    public Animator targetAvatar;
    [Tooltip("Define the start position of animation.")]
    public GameObject startPos;
    [Space]

    [Header("External File Paths")]
    [Tooltip("This is the path to the file which describes Bonemaps between unity and bvh.")]
    public string bonemapPath = @"Assets/Scripts/0_BoneMaps/Bonemaps_CMU.txt";
    [Tooltip("This is the path to the BVH file that should be loaded. Bone offsets are currently being ignored by this loader.")]
    public string BVHFilename;
    [Space]

    [Header("Frame Rate Controller")]
    [Tooltip("This is the overall refresh rate of project.")]
    public float environmentFrameRate = 24.0f;
    [Tooltip("This is the refresh rate of cartoon animation motion.")]
    public float animationFrameRate = 24.0f;
    [SerializeField]
    [Tooltip("If this flag is deactivated, the playing rate follows the original frame rate defined in BVH File.")]
    private bool ifUsingCustomizedRate = true;
    [ConditionalHide(nameof(ifUsingCustomizedRate), false), SerializeField]
    [Tooltip("If the flag above is activated, this is the playing speed of FixedUpdate.")]
    private float playRate = 50.0f;
    
    [Serializable]
    public struct BoneMap       // the corresponding bones between unity and bvh
    {
        public string bvh_name;
        public HumanBodyBones humanoid_bone;
    }
    [Space]
    [Header("Bonemap List")]
    public BoneMap[] bonemaps; 


    /***********************************  private 变量  ****************************************/
    private BVHParser bp = null;
    private float frameRate;
    private float originalFrameRate;
    private float frameRatio;
    // 该函数不调用任何 Unity API 函数，因此可以安全地从另一个线程调用
    public void parseFile()
    {
        string bvhData = File.ReadAllText(BVHFilename);
        bp = new BVHParser(bvhData);
        // frameRate = 1f / bp.frameTime;
        originalFrameRate = 1f / bp.frameTime;
        frameRate = environmentFrameRate;
    }
    private Animator anim;
    private Camera currentCamera;
    private Dictionary<string, Quaternion> bvhT;
    private Dictionary<string, Vector3> bvhOffset;
    private Dictionary<string, string> bvhHireachy;
    private Dictionary<HumanBodyBones, Quaternion> unityT;
    private int frameIdx;
    private float scaleRatio = 0.0f;
    private float nearRatio = 0.0f;

    private void Start()
    {
        /********************************  解析外部文件  *************************************/
        // set mapping between bvh_name and humanBodyBones
        BonemapReader.Read(bonemapPath);        // 调用BoneReader.Read，解析BVH文件
        bonemaps = BonemapReader.bonemaps;      // 设置 bvh_name 和 humanBodyBones 之间的映射关系
        parseFile();

        /************************  计算并设置项目帧率和动作播放帧率  ****************************/
        Application.targetFrameRate = (Int16)frameRate;
        if(ifUsingCustomizedRate == false)
        {
            Time.fixedDeltaTime = 1 / originalFrameRate;
        }
        else
        {
            Time.fixedDeltaTime = 1 / playRate;
        }
        frameRatio = originalFrameRate / animationFrameRate;

        /****************************  初始化骨骼和关键帧数据  ********************************/
        bvhT = bp.getKeyFrame(0);       // 获取bvh文件第0帧位置的BoneData，即bvh骨骼的Tpose信息
        bvhOffset = bp.getOffset(1.0f); 
        bvhHireachy = bp.getHierachy(); // 获得bvh文件的骨架结构信息，包含骨骼名称和骨骼节点的父子关系
        anim = targetAvatar.GetComponent<Animator>();   // 获取目标角色模型的Animator组件，用于后续对角色动画的控制
        unityT = new Dictionary<HumanBodyBones, Quaternion>();
        foreach (BoneMap bm in bonemaps)    // 遍历骨骼映射数据
        {
            // 将Tpose下，unity对象内置骨骼的名称和相对世界坐标系的旋转信息记录在unityT中
            unityT.Add(bm.humanoid_bone, anim.GetBoneTransform(bm.humanoid_bone).rotation);
        }
        frameIdx = 1;

        /****************************  计算重定向中的骨骼缩放  ********************************/
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
        scaleRatio = unity_leftleg / bvh_leftleg;   // 得到骨骼缩放比例
    }

    private void FixedUpdate()
    {
        /**********************   获取当前帧和上一临近帧的骨骼数据 boneData   **********************/
        Dictionary<string, Quaternion> currFrame = bp.getKeyFrame(frameIdx);
        Dictionary<string, Quaternion> lastFrame = bp.getKeyFrame(frameIdx - 1);
        if (frameIdx % (Int16)frameRatio == 1)
        {
            /****************************   更新骨骼关节节点旋转   *********************************/
            // 根据bvh中骨骼关节点的local rotation，更新unity中的animation骨骼相对世界坐标系的旋转rotation
            BVHLocalRotationToUnityRotation(currFrame, bonemaps, bvhT, unityT);  
        }
        /**********************   将BVH中的人物模型位移转换为unity 2D 运动规范   ******************/
        Dictionary<string, Vector3> currBVHPos = getBVHPos(currFrame, bvhHireachy, bvhOffset);
        Dictionary<string, Vector3> lastBVHPos = getBVHPos(lastFrame, bvhHireachy, bvhOffset);
        BVHPosToUnityMovement(currBVHPos, lastBVHPos, frameIdx);

        /*****************************   动画播放与循环   **********************************/
        frameIdx = (frameIdx < bp.frames - 1) ? frameIdx + 1 : 1;
    }

    // 返回在currFrame帧位置，各个关节节点在世界空间中的坐标
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

    /********************************************************
     * BVHPosToUnityMovement：
     * 通过当前帧和上一帧中各个关节节点在世界空间坐标中的坐标位置，驱动unity人形动画移动
     * 
     * moveDirection:   2D动作运动目标方向
     * { Forward2D: 2D角色前后方向，
     *   Up2D: 2D角色上下方向，
     *   Near2D: 2D角色z轴深度方向}
     * characterDirection:  人物模型自身方向定义
     * currVelocity:    根节点的平均位移速度
     * ******************************************************************/
    private void BVHPosToUnityMovement(Dictionary<string, Vector3> currBVHPos, Dictionary<string, Vector3> lastBVHPos, int frameIndex)
    {
        /************************    准备基础的速度和方向参数    ***************************/
        Dictionary<string, Vector3> moveDirection = get2DWorldDirection();
        Dictionary<string, Vector3> characterDirection = get3DCharacterDirection(); 
        Vector3 currVelocity = getAverageVelocity(currBVHPos, lastBVHPos);
        // 将速度投影到character自身的三维方向
        Vector3 forwardVelocity = Vector3.Project(currVelocity, characterDirection["Forward3D"]);
        Vector3 upVelocity = Vector3.Project(currVelocity, characterDirection["Up3D"]);
        Vector3 nearVelocity = Vector3.Project(currVelocity, characterDirection["Near3D"]);
        
        /************************    更新根节点在x和y方向的位移    ***************************/
        // Debug: 绘制当前位移速度方向
        Debug.DrawRay(anim.GetBoneTransform(HumanBodyBones.Hips).position, forwardVelocity * 1000f, Color.red);
        Debug.DrawRay(anim.GetBoneTransform(HumanBodyBones.Hips).position, upVelocity * 1000f, Color.blue);
        Debug.DrawRay(anim.GetBoneTransform(HumanBodyBones.Hips).position, nearVelocity * 1000f, Color.green);
        // 根据帧和速度在特定方向上更新根节点位移
        anim.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.Dot(forwardVelocity.normalized, moveDirection["Forward2D"].normalized) * forwardVelocity.magnitude * moveDirection["Forward2D"] * bp.frameTime;
        anim.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.Dot(upVelocity.normalized, moveDirection["Up2D"].normalized) * upVelocity.magnitude * moveDirection["Up2D"] * bp.frameTime;

        /************************    更新人物模型在z轴向的缩放    ***************************/
        // 使用投影Near2D方向的方法更新深度影响的scale
        // 用相机矩阵计算nearRatio，然后调整加减正负的大小问题
        Vector3 characterHip = anim.GetBoneTransform(HumanBodyBones.Hips).transform.position;
        nearRatio = Camera_GetVerticalDistance_Character(characterHip);
        float delta_s = Vector3.Dot(nearVelocity.normalized, moveDirection["Near2D"].normalized) * nearVelocity.magnitude * bp.frameTime;
        float nearScale = Vector3.Dot(nearVelocity.normalized, moveDirection["Near2D"].normalized) * (nearRatio / nearRatio + delta_s);
        float scaleSpeed = 0.0005f;
        Vector3 newScale = new Vector3(gameObject.transform.localScale.x * nearScale, gameObject.transform.localScale.y * nearScale, gameObject.transform.localScale.z * nearScale);
        gameObject.transform.localScale += newScale * scaleSpeed;

        /************************    控制动画循环播放    ***********************************/
        if (frameIndex == 1)
        {
            anim.GetBoneTransform(HumanBodyBones.Hips).position = startPos.transform.position;
            gameObject.transform.localScale = startPos.transform.localScale;
        }
    }



    /************************   控制z轴缩放 相关的函数   *************************************/
    // 得到人物模型与当前相机的绝对距离
    private float Camera_GetVerticalDistance_Character(Vector3 characterHip)
    {
        currentCamera = UnityEngine.Camera.main;
        Plane cameraPlane = new Plane(currentCamera.transform.forward, currentCamera.transform.position);
        float distance = cameraPlane.GetDistanceToPoint(characterHip);
        return distance;
    }


    /*********************   更新骨骼关节节点旋转和位置更新 相关的函数   *************************/
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

    /************************   更新人物模型位移和朝向 相关的函数   ******************************/
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




    /******************************   原BVH Parser的动作驱动   *************************************/
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
}
